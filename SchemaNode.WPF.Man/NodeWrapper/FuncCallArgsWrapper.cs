using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.WPF.Man.Services;
using SchemaNode.WPF.View;
using SchemaNode.WPF.View.NodeWrapper;

namespace SchemaNode.WPF.Man.NodeWrapper;

/// <summary>
/// Read-only presentation of <see cref="FuncCallArgsNode"/> (counterpart of callArgs.vue).
///
/// The argument list is not an editable array: Core rebuilds it from the selected function's
/// signature. This wrapper therefore exposes no add/remove commands. Membership is reconciled
/// from <see cref="FuncCallArgsNode.SubscribeArgs"/>, which Core raises whenever the function
/// (or call mode) changes and the argument nodes are replaced, so switching functions clears
/// stale rows instead of appending to them.
///
/// Individual argument nodes stay wrapped by the normal factory, so the fields Core marks
/// editable (source/value per call mode) remain editable.
/// </summary>
public sealed class FuncCallArgsWrapper : DataNodeWrapper
{
    private readonly Dictionary<DataNode, DataNodeWrapper> _childByNode = [];
    private bool _reconciling;

    public FuncCallArgsWrapper(DataNode node, SchemaViewRegistry? registry, string? fieldName, SchemaViewRole role,
                               bool ancestorVisibility = true)
        : base(node, registry, fieldName, role, ancestorVisibility)
    {
        ArgsNode = node as FuncCallArgsNode
            ?? throw new ArgumentException($"Node '{node.Type.Name}' is not a function call argument list.", nameof(node));

        ReconcileChildren();
        // SubscribeArgs is the membership channel. Subscribe() alone would miss a function switch,
        // because Core replaces the argument nodes without writing the list value.
        Track(ArgsNode.SubscribeArgs(_ => Post(ReconcileChildren)));
        Track(ArgsNode.Subscribe(OnArgsValueChanged));
    }

    private FuncCallArgsNode ArgsNode { get; }

    /// <inheritdoc />
    public override string? DefaultTemplateKey => ManTemplateKeys.CallArgs;

    /// <summary>The argument list is a grouping container; failures belong to its leaf fields.</summary>
    protected override bool RendersValue => false;

    /// <summary>Argument wrappers in signature order, including variadic arguments flattened.</summary>
    public BulkObservableCollection<DataNodeWrapper> Children { get; } = new();

    /// <summary>Whether the selected function has no arguments (or no function is selected yet).</summary>
    public bool IsEmpty => Children.Count == 0;

    private void OnArgsValueChanged((IValueAccess node, object? value) args)
    {
        if (_reconciling) return;
        Post(ReconcileChildren);
    }

    private void ReconcileChildren()
    {
        if (IsDisposed) return;
        _reconciling = true;
        try
        {
            List<DataNodeWrapper> next = [];
            HashSet<DataNode> alive = [];

            foreach (IValueAccess item in ArgsNode)
            {
                if (item is not DataNode element) continue;
                alive.Add(element);

                DataNodeWrapper wrapper = _childByNode.TryGetValue(element, out DataNodeWrapper? existing)
                    ? existing
                    : NodeWrapperFactory.Create(element, Registry, null, Role, ancestorVisibility: false);
                wrapper.SetForcedReadOnly(IsForcedReadOnly);
                _childByNode[element] = wrapper;
                next.Add(wrapper);
            }

            foreach (DataNode key in _childByNode.Keys.Where(k => !alive.Contains(k)).ToList())
            {
                _childByNode[key].Dispose();
                _childByNode.Remove(key);
            }

            if (next.Count == Children.Count && next.SequenceEqual(Children))
            {
                RaisePropertyChangedNow(nameof(IsEmpty));
                return;
            }

            Children.ReplaceAll(next);
            RaisePropertyChangedNow(nameof(IsEmpty));
        }
        finally
        {
            _reconciling = false;
        }
    }

    /// <inheritdoc />
    protected override void OnForcedReadOnlyChanged(bool value)
    {
        foreach (DataNodeWrapper child in Children)
            child.SetForcedReadOnly(value);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !IsDisposed)
        {
            foreach (DataNodeWrapper child in Children)
                child.Dispose();
            Children.ReplaceAll([]);
            _childByNode.Clear();
        }
        base.Dispose(disposing);
    }
}
