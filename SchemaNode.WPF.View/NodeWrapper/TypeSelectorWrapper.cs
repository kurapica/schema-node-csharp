using System.ComponentModel;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Utility;
using SchemaNode.WPF.View.Localization;
// ReSharper disable once InconsistentNaming
using NodeType = SchemaNode.Runtime.NodeType;

namespace SchemaNode.WPF.View.NodeWrapper;

/// <summary>One generic type argument editor of a type path node.</summary>
public sealed class TypeArgument
{
    internal TypeArgument(string name, TypeSelectorWrapper wrapper, bool isLast)
    {
        Name = name;
        Wrapper = wrapper;
        IsLast = isLast;
    }

    /// <summary>Declared generic parameter name, e.g. <c>T</c>.</summary>
    public string Name { get; }

    /// <summary>The editor wrapper of the argument value (itself a type path node).</summary>
    public TypeSelectorWrapper Wrapper { get; }

    /// <summary>Whether this is the last argument of the list.</summary>
    public bool IsLast { get; }

    /// <summary>Whether a comma separator is rendered after the argument.</summary>
    public bool ShowComma => !IsLast;
}

/// <summary>
/// Wraps a type path node and splits it into the plain path plus its generic arguments,
/// mirroring <c>schema-node-man/src/view/namespace.vue</c>.
///
/// The node value keeps the composed form (<c>system.list&lt;system.string&gt;</c>). The path part is
/// edited through a child wrapper whose node has the same type as the host node, so the entry source
/// (namespace / type cascade) is reused as-is; every generic argument is a child node of type
/// <c>system.schema.node.valuetype</c>, recursively wrapped by this class.
/// </summary>
public sealed class TypeSelectorWrapper : DataNodeWrapper
{
    private static readonly string ValueTypeNodeType = $"{Constant.NS_SYSTEM_SCHEMA_NODE}.valuetype";

    private readonly BulkObservableCollection<TypeArgument> _arguments = [];
    private readonly List<IDisposable> _ownedNodes = [];
    private InputWrapper? _main;
    private string? _pendingMain;
    private string? _genericMain;
    private string? _lastWritten;
    private bool _hasGenerics;
    private bool _syncing;
    private int _revision;
    private int _writeRevision;

    /// <summary>Creates the wrapper for a type path node.</summary>
    public TypeSelectorWrapper(DataNode node, SchemaViewRegistry? registry, string? fieldName, SchemaViewRole role,
                               bool ancestorVisibility = true)
        : base(node, registry, fieldName, role, ancestorVisibility)
    {
        ScheduleRebuild();
    }

    /// <inheritdoc />
    public override string? DefaultTemplateKey => SchemaTemplateKeys.TypeSelector;

    /// <summary>The plain path editor (namespace / type cascade), without the generic part.</summary>
    public InputWrapper? Main => _main;

    /// <summary>The generic argument editors, empty for a non-generic type.</summary>
    public BulkObservableCollection<TypeArgument> Arguments => _arguments;

    /// <summary>Whether the resolved path names a generic template and arguments are shown.</summary>
    public bool HasGenerics => _hasGenerics;

    /// <inheritdoc />
    protected override void OnValueChanged()
    {
        ScheduleRebuild();
    }

    /// <inheritdoc />
    protected override void OnEditableChanged()
    {
        if (_main is not null)
            _main.SetForcedReadOnly(!IsEditable);
        if (_arguments is null)
            return;
        foreach (TypeArgument argument in _arguments)
            argument.Wrapper.SetForcedReadOnly(!IsEditable);
    }

    /// <inheritdoc />
    protected override void OnForcedReadOnlyChanged(bool value)
    {
        if (_arguments is null)
            return;
        foreach (TypeArgument argument in _arguments)
            argument.Wrapper.SetForcedReadOnly(!IsEditable);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !IsDisposed)
        {
            _revision++;
            _writeRevision++;
            _main?.Dispose();
            _main = null;
            ResetArguments([]);
            DisposeOwnedNodes();
        }
        base.Dispose(disposing);
    }

    #region Rebuild

    private void ScheduleRebuild(string? main = null)
    {
        if (IsDisposed) return;
        _revision++;
        if (main is not null) _pendingMain = main;
        int revision = _revision;
        Post(() => _ = RebuildAsync(revision));
    }

    private async Task RebuildAsync(int revision)
    {
        if (IsDisposed || revision != _revision) return;

        string raw = RawValue();
        string? pending = _pendingMain;
        _pendingMain = null;

        (string parsedMain, string[] parsedArgs) = SplitGeneric(raw);
        string main = pending ?? parsedMain;
        // A pending value comes from the path editor: show it even when the stored value no longer
        // parses (broken path). For an external value the full raw text is shown when it is not a
        // generic template, matching namespace.vue (the node itself is the main node there).
        bool external = pending is null && !string.Equals(raw, _lastWritten, StringComparison.Ordinal);

        NodeType? type = null;
        if (main.Length > 0 && NodeImpl.Context is SchemaContext context)
        {
            try
            {
                type = await context.GetNodeTypeAsync<NodeType>(main);
            }
            catch
            {
                type = null;
            }
        }
        if (IsDisposed || revision != _revision) return;

        IValueTypeAccess? argumentType = null;
        if (type is { IsGeneric: true } && NodeImpl.Context is SchemaContext argumentContext)
        {
            try
            {
                argumentType = await argumentContext.GetNodeTypeAsync<IValueTypeAccess>(ValueTypeNodeType);
            }
            catch
            {
                argumentType = null;
            }
        }
        if (IsDisposed || revision != _revision) return;

        Apply(raw, main, pending is not null, parsedArgs, type, argumentType, external);
    }

    private void Apply(string raw, string main, bool pending, string[] parsedArgs, NodeType? type,
        IValueTypeAccess? argumentType, bool external)
    {
        _syncing = true;
        try
        {
            bool generic = type is { IsGeneric: true } && argumentType is not null;
            string mainText = generic || pending ? main : raw;

            EnsureMain(mainText);

            if (!generic)
            {
                if (_arguments.Count > 0) ResetArguments([]);
                _genericMain = null;
                SetGenerics(false);
                return;
            }

            IReadOnlyList<GenericParameter> parameters = type!.Generics!;
            bool sameShape = string.Equals(_genericMain, main, StringComparison.Ordinal) &&
                             _arguments.Count == parameters.Count;
            if (sameShape)
            {
                if (external) ReseedArguments(parsedArgs);
                return;
            }

            List<TypeArgument> next = [];
            for (int i = 0; i < parameters.Count; i++)
            {
                GenericParameter parameter = parameters[i];
                string? initial = i < parsedArgs.Length ? parsedArgs[i] : null;
                if (argumentType!.Create(NodeImpl.Context, initial, NodeImpl) is not DataNode argumentNode) continue;
                _ownedNodes.Add(argumentNode);
                TypeSelectorWrapper wrapper = new(argumentNode, Registry, FieldName, Role);
                wrapper.PropertyChanged += OnArgumentChanged;
                wrapper.SetForcedReadOnly(!IsEditable);
                next.Add(new TypeArgument(parameter.Name, wrapper, i == parameters.Count - 1));
            }
            ResetArguments(next);
            _genericMain = main;
            SetGenerics(true);
        }
        finally
        {
            _syncing = false;
        }
    }

    private void EnsureMain(string mainText)
    {
        if (_main is null)
        {
            if (NodeImpl.Type.Create(NodeImpl.Context, mainText, NodeImpl) is not DataNode mainNode)
            {
                // An unreadable path (or a context that cannot create the child) would leave the
                // field as an empty presenter with no explanation; surface the failure instead of
                // dropping the value silently.
                Post(() => SetInputError(
                    LocaleManager.Instance.Format("wpf.error.value.convert", mainText, NodeImpl.Type.Name)));
                return;
            }
            _ownedNodes.Add(mainNode);
            InputWrapper wrapper = new(mainNode, Registry, FieldName, Role, ancestorVisibility: false);
            wrapper.PropertyChanged += OnMainChanged;
            wrapper.SetForcedReadOnly(!IsEditable);
            _main = wrapper;
            RaisePropertyChangedNow(nameof(Main));
            return;
        }

        if (!string.Equals(AsText(_main.Value), mainText, StringComparison.Ordinal))
            _main.Value = mainText;
    }

    private void ReseedArguments(string[] parsedArgs)
    {
        for (int i = 0; i < _arguments.Count; i++)
        {
            string next = i < parsedArgs.Length ? parsedArgs[i] : "";
            TypeArgument argument = _arguments[i];
            if (!string.Equals(AsText(argument.Wrapper.Value), next, StringComparison.Ordinal))
                argument.Wrapper.Value = next;
        }
    }

    private void ResetArguments(IEnumerable<TypeArgument> next)
    {
        foreach (TypeArgument argument in _arguments)
        {
            argument.Wrapper.PropertyChanged -= OnArgumentChanged;
            DataNode? node = argument.Wrapper.Node;
            argument.Wrapper.Dispose();

            // The argument node was created for this shape and is owned by this wrapper. Releasing
            // it together with the wrapper keeps repeated generic switches from accumulating nodes.
            if (node is not null && _ownedNodes.Remove(node))
                node.Dispose();
        }
        _arguments.ReplaceAll(next);
        RaisePropertyChangedNow(nameof(Arguments));
    }

    private void SetGenerics(bool value)
    {
        if (_hasGenerics == value) return;
        _hasGenerics = value;
        RaisePropertyChangedNow(nameof(HasGenerics));
    }

    private void DisposeOwnedNodes()
    {
        for (int i = _ownedNodes.Count - 1; i >= 0; i--)
            _ownedNodes[i].Dispose();
        _ownedNodes.Clear();
    }

    #endregion

    #region Write back

    private void OnMainChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Value) || _syncing || IsDisposed) return;
        // Rebuild first so a generic template immediately exposes its argument editors, then commit
        // the plain path (a generic path is only written once every argument is complete).
        ScheduleRebuild(AsText(_main?.Value));
        _ = WriteBackAsync();
    }

    private void OnArgumentChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Value) || _syncing || IsDisposed) return;
        _ = WriteBackAsync();
    }

    private async Task WriteBackAsync()
    {
        if (IsDisposed || _syncing) return;

        string main = AsText(_main?.Value);
        if (main.Length == 0)
        {
            // Clearing the path clears the node; an optional type field can be emptied this way.
            SetNodeValue("");
            return;
        }

        int revision = ++_writeRevision;
        NodeType? type = null;
        if (NodeImpl.Context is SchemaContext context)
        {
            try
            {
                type = await context.GetNodeTypeAsync<NodeType>(main);
            }
            catch
            {
                type = null;
            }
        }
        if (IsDisposed || revision != _writeRevision) return;

        if (type is null || !type.IsGeneric)
        {
            SetNodeValue(main);
            return;
        }

        int declared = type.Generics?.Count ?? 0;
        if (declared == 0 || _arguments.Count != declared) return; // argument editors are still being built

        string[] values = _arguments.Select(argument => AsText(argument.Wrapper.Value)).ToArray();
        if (values.Any(string.IsNullOrEmpty)) return; // incomplete instantiation: keep the node value

        SetNodeValue($"{main}<{string.Join(",", values)}>");
    }

    private void SetNodeValue(string value)
    {
        if (string.Equals(RawValue(), value, StringComparison.Ordinal)) return;
        _lastWritten = value;
        Value = value;
    }

    #endregion

    #region Value helpers

    private string RawValue() => NodeImpl.GetValue<string>() ?? "";

    private static string AsText(object? value) => value switch
    {
        null => "",
        string text => text,
        _ => value.ToString() ?? "",
    };

    /// <summary>
    /// Splits <c>main&lt;a,b&gt;</c> into the path before the first <c>&lt;</c> and the top-level
    /// arguments. Mirrors the vue <c>splitGenericParams</c> usage in <c>namespace.vue</c>.
    /// </summary>
    internal static (string Main, string[] Args) SplitGeneric(string text)
    {
        string value = text.Trim();
        int start = value.IndexOf('<');
        if (start < 0) return (value, []);

        int end = value.LastIndexOf('>');
        if (end <= start + 1) return (value[..start].Trim(), []);

        return (value[..start].Trim(), SplitTopLevel(value[(start + 1)..end]));
    }

    private static string[] SplitTopLevel(string text)
    {
        List<string> result = [];
        int depth = 0;
        int begin = 0;
        for (int i = 0; i < text.Length; i++)
        {
            switch (text[i])
            {
                case '<':
                    depth++;
                    break;
                case '>':
                    depth--;
                    break;
                case ',' when depth == 0:
                    result.Add(text[begin..i].Trim());
                    begin = i + 1;
                    break;
            }
        }

        string tail = text[begin..].Trim();
        if (tail.Length > 0 || result.Count > 0) result.Add(tail);
        return result.ToArray();
    }

    #endregion
}
