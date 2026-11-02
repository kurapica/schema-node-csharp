using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.String;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Struct;
using SchemaNode.Utility;
using System.Collections;
using System.Reactive;
using System.Reactive.Subjects;
using System.Text.Json.Nodes;
using static SchemaNode.Utility.Constant;
using ArrayType = SchemaNode.Runtime.ArrayType;
using StructType = SchemaNode.Runtime.StructType;

namespace SchemaNode.Node;

/// <summary>
/// The function expression arguments data node
/// </summary>
public class FuncCallArgsNode : DataNode, IEnumerable<StructNode>
{
    #region Fields

    private readonly List<StructNode> _args = [];
    private readonly StructType _argType;
    private FuncCallVarNode? _varArg;
    private FunctionType? _funcType;
    private IValueAccess? _return;
    private ApplyMode? _mode;
    private JsonArray? _initData;
    private JsonArray? _original;
    private Subject<(IValueAccess, int)>? _argCountOb;
    private int _preColIdx = -1;

    #endregion

    #region Constructor

    public FuncCallArgsNode(ISchemaContext context, IValueTypeAccess type, object? value = null, IValueAccess? parent = null, params IPropertyProvider[] propertyProviders)
        : base(context, type, null, parent, propertyProviders)
    {
        _argType = (type as ArrayType)?.Element as StructType
            ?? throw new InvalidCastException($"The type '{type.Name}' is not a valid function call arguments type.");

        // waiting for the parent function call refresh
        _initData = FuncCallHelper.ToJsonArray(value);
    }

    #endregion

    #region Methods

    /// <summary>
    /// Refresh the function call arguments with the given apply mode and function type
    /// </summary>
    public void RefreshFuncCall(ApplyMode? mode, FunctionType? funcType)
    {
        if (mode == _mode && funcType == _funcType) return;
        _funcType = funcType;
        _mode = mode;
        RebuildArgs();
    }

    #endregion

    #region Value Access

    /// <inheritdoc/>
    public override bool IsEmpty => _args.Count == 0 && (_varArg?.IsEmpty ?? true);

    /// <inheritdoc/>
    public override bool TrySetValue<T>(T? value) where T : default
    {
        if (value is null)
        {
            _initData = null;
            RebuildArgs();
            OnNext(BuildJsonArray());
            return true;
        }

        JsonArray? data = FuncCallHelper.ToJsonArray(value);
        if (data is null) return false;

        _initData = data;
        RebuildArgs();
        return true;
    }

    /// <inheritdoc/>
    public override bool TryGetValue(Type type, out object? value)
    {
        if (type == typeof(CallArg[]) || type.IsArray && type.GetElementType() == typeof(CallArg))
        {
            value = BuildCallArgs();
            return true;
        }

        JsonArray array = _initData is not null ? (JsonArray)_initData.DeepClone() : BuildJsonArray();
        if (type == typeof(JsonArray) || type == typeof(JsonNode))
        {
            value = array;
            return true;
        }

        if (type == typeof(string))
        {
            value = array.ToJsonString();
            return true;
        }

        if (type == typeof(object) || type.IsAssignableFrom(GetType()))
        {
            value = this;
            return true;
        }

        if (type == typeof(IEnumerable))
        {
            value = array.ToList();
            return true;
        }

        value = null;
        return false;
    }

    /// <inheritdoc/>
    public override bool TryGetOriginal(Type type, out object? value)
    {
        if (_original is null)
        {
            value = null;
            return false;
        }

        if (type == typeof(JsonArray) || type == typeof(JsonNode))
        {
            value = _original.DeepClone();
            return true;
        }

        if (type == typeof(string))
        {
            value = _original.ToJsonString();
            return true;
        }

        try
        {
            value = _original.FromJson(type);
            return value != null;
        }
        catch
        {
            value = null;
            return false;
        }
    }

    /// <inheritdoc/>
    public override bool Changed => _args.Any(e => e.Changed) || (_varArg?.Changed ?? false);

    /// <inheritdoc/>
    public override bool IsValid => _args.All(e => e.IsValid) && (_varArg?.IsValid ?? true) && base.IsValid;

    /// <inheritdoc/>
    public override JsonNode? Violated
    {
        get
        {
            if (IsValid) return null;
            List<JsonNode?> items = _args.Select(e => (JsonNode?)(e.Violated ?? JsonValue.Create(true))).ToList();
            if (_varArg is not null) items.Add(_varArg.Violated ?? JsonValue.Create(true));
            return new JsonArray(items.ToArray());
        }
    }

    /// <inheritdoc/>
    public override IEnumerable<DataNode> GetErrorNodes()
    {
        foreach (StructNode node in _args)
            foreach (DataNode error in node.GetErrorNodes())
                yield return error;
        if (_varArg is not null)
            foreach (DataNode error in _varArg.GetErrorNodes())
                yield return error;
        foreach (DataNode error in base.GetErrorNodes())
            yield return error;
    }

    /// <inheritdoc/>
    public override void Confirm()
    {
        foreach (StructNode node in _args)
            node.Confirm();
        _varArg?.Confirm();
        _original = BuildJsonArray();
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        if (_original is null)
            ClearValue();
        else
            TrySetValue<object>(_original);
    }

    /// <inheritdoc/>
    public override void ClearValue()
    {
        _initData = null;
        _args.Clear();
        _varArg = null;
        _preColIdx = -1;
        OnNext(BuildJsonArray());
    }

    /// <inheritdoc/>
    public override IValueAccess Clone()
    {
        FuncCallArgsNode clone = new(Context, Type, BuildJsonArray(), null, PropertyProviders.ToArray());
        clone._original = _original?.DeepClone() as JsonArray;
        return clone;
    }

    #endregion

    #region Dispose

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        _argCountOb?.Dispose();
        _argCountOb = null;

        foreach (StructNode arg in _args)
            arg.Dispose();
        _args.Clear();

        _varArg?.Dispose();
        _varArg = null;

        base.Dispose(disposing);
    }

    #endregion

    #region Args Changed

    /// <summary>
    /// Subscribe the argument count change, normally for the element nodes
    /// </summary>
    public IDisposable SubscribeArgs(Action<(IValueAccess, int)> handler, bool immediate = false)
    {
        _argCountOb ??= new Subject<(IValueAccess, int)>();
        if (immediate) handler((this, Length));
        return _argCountOb.Subscribe(items => handler((items.Item1, items.Item2)));
    }

    /// <summary>
    /// Publish the argument count change
    /// </summary>
    public void OnNextArgs()
    {
        if (!FullMode) return;
        _argCountOb?.OnNext((this, Length));
    }

    #endregion

    #region Path Navigation

    /// <inheritdoc/>
    public override IValueAccess? GetAccessValue(string path, IValueAccess? node = null)
    {
        if (base.GetAccessValue(path, node) is { } v) return v;
        if (string.IsNullOrEmpty(path)) return null;

        if (path.Equals(FUNC_RETURN, StringComparison.OrdinalIgnoreCase))
            return _return ??= Parent?.GetAccessValue(FUNC_RETURN);

        string[] paths = path.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
        if (paths[0].Equals(ARRAY_ELEMENT, StringComparison.OrdinalIgnoreCase))
        {
            IValueAccess? item = node;
            while (item is not null && item.Parent != this)
                item = item.Parent;
            return paths.Length <= 1 ? item : item?.GetAccessValue(paths[1], node);
        }

        DataNode? arg = At(paths[0]);
        if (arg is null) return null;
        if (arg is FuncCallVarNode)
            return paths.Length <= 1 ? arg : null; // only support direct access for variadic argument
        return paths.Length <= 1 ? arg : arg.GetAccessValue(paths[1]);
    }

    #endregion

    #region Iterable

    /// <summary>
    /// Gets the argument count, including the variadic arguments
    /// </summary>
    public int Length => _args.Count + (_varArg?.Count ?? 0);

    /// <inheritdoc/>
    public IEnumerator<StructNode> GetEnumerator()
    {
        foreach (StructNode node in _args)
            yield return node;
        if (_varArg is not null)
            foreach (StructNode node in _varArg)
                yield return node;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Gets the argument at the given index or name
    /// </summary>
    public DataNode? At(int index)
        => index >= _args.Count ? _varArg?.At(index - _args.Count) : index >= 0 ? _args[index] : null;

    /// <summary>
    /// Gets the argument by name
    /// </summary>
    public DataNode? At(string name)
        => (DataNode?)_args.FirstOrDefault(e => e.Name?.Equals(name, StringComparison.OrdinalIgnoreCase) == true)
           ?? (_varArg?.Name?.Equals(name, StringComparison.OrdinalIgnoreCase) == true ? _varArg : null);

    /// <summary>
    /// Gets the variadic argument
    /// </summary>
    public FuncCallVarNode? VariadicArg => _varArg;

    #endregion

    #region Utility

    private StructNode? AddRow(JsonObject data, string name, IPropertyProvider? provider)
    {
        IValueAccess created = provider is null
            ? _argType.Create(Context, null, this)
            : _argType.Create(Context, null, this, provider);
        if (created is not StructNode node) return null;
        if (!node.TrySetValue(data)) return null;

        _args.Add(node);
        node.SetPropertyValue<Name, string>(name, this, null);
        if (FullMode) node.ApplyPropertyEffects();
        return node;
    }

    private void RebuildArgs()
    {
        JsonArray data = _initData ?? BuildJsonArray();
        bool isReadonly = false;
        DataNode node = this;
        while (!node.GetPropertyValue<ReadOnly, bool>() && node.Parent is DataNode parent)
            node = parent;
        isReadonly = node.GetPropertyValue<ReadOnly, bool>();

        _args.Clear();
        _varArg = null;
        _preColIdx = -1;

        if (_funcType is null || _funcType.IsGeneric) return;

        _initData = null;

        IValueAccess? defineNode = FindDefineNode();
        for (int i = 0; i < _funcType.Args.Length; i++)
        {
            FunctionNodeArgument arg = _funcType.Args[i];
            string argName = GetArgName(arg);

            if (arg.Variadic == true)
            {
                JsonArray adata = new(data.Skip(i).Select(n => n?.DeepClone()).ToArray());
                if (adata.Count == 0 && !isReadonly)
                    adata.Add(new JsonObject { ["type"] = arg.Type });
                for (int j = 0; j < adata.Count; j++)
                {
                    if (adata[j] is JsonObject obj) obj["type"] = arg.Type;
                    else adata[j] = new JsonObject { ["type"] = arg.Type };
                }

                FuncCallVarNode varArg = new(Context, _argType, adata, this, arg);
                if (isReadonly)
                    varArg.SetPropertyValue<ReadOnly, bool>(true, null, null);
                else if (FullMode)
                {
                    varArg.RecordSubscription(varArg.SubscribeArgs(_ => OnNextArgs()), varArg);
                    varArg.RecordSubscription(varArg.Subscribe(_ => WriteBackRawValue()), varArg);
                }
                _varArg = varArg;

                if (_mode != ApplyMode.Call)
                {
                    varArg.SetCollectionSelectable(true);
                    if (FullMode)
                        varArg.RecordSubscription(varArg.Subscribe(_item => { _ = RefreshArgTypesAsync(); }), varArg);
                }
                break; // variadic arguments are the last
            }

            JsonObject adata2 = data.ElementAtOrDefault(i) is JsonObject row ? (JsonObject)row.DeepClone() : new JsonObject();
            adata2["type"] = arg.Type;
            StructNode argNode = AddRow(adata2, argName, arg)!;

            if (isReadonly)
                argNode.SetPropertyValue<ReadOnly, bool>(true, null, null);
            else
            {
                if (FullMode)
                    argNode.RecordSubscription(argNode.Subscribe(_ => WriteBackRawValue()), argNode);

                if (_mode != ApplyMode.Call)
                {
                    IValueAccess? source = argNode.GetAccessValue("source");
                    if (defineNode is not null)
                        source?.SetPropertyValue<EntrySource, FuncCall>(
                            FuncCallHelper.Build($"{NS_SYSTEM_SCHEMA_REFLECT_FUNC}.getaccessentries", "@args", $"@exps.{ARRAY_PREVIOUS}", true, NODE_SELF),
                            defineNode,
                            null);
                    source?.SetPropertyValue<AccessEntryConsumer, FuncCall>(
                        FuncCallHelper.Build($"{NS_SYSTEM_SCHEMA_REFLECT_TYPE}.isassignableto", NODE_SELF, true, $"@{ARRAY_ELEMENT}.type"),
                        argNode.Parent,
                        null);

                    if (FullMode)
                        argNode.RecordSubscription(argNode.Subscribe(_item => { _ = RefreshArgTypesAsync(); }), argNode);
                }
            }
        }

        // attach the function relations
        foreach (StructNode arg in _args)
            ApplyFuncRelations(arg);
        if (_varArg is not null)
            ApplyFuncRelations(_varArg);

        OnNextArgs();
    }

    private async Task RefreshArgTypesAsync()
    {
        if (!FullMode || Context is not SchemaContext ctx) return;

        IValueAccess? defineNode = FindDefineNode();
        int colIdx = -1;
        for (int i = 0; i < _args.Count; i++)
        {
            JsonObject? raw = GetArgJson(_args[i]);
            string? typeName = raw?["type"]?.GetValue<string>();
            string? source = raw?["sourceType"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(typeName) || source == typeName)
                continue;

            if (await SchemaNode.Function.Reflect.Type.isassignableto(ctx, source, false, typeName))
                continue;
            if (await SchemaNode.Function.Reflect.Type.isassignableto(ctx, source, true, typeName))
            {
                colIdx = i;
                break;
            }
        }

        if (colIdx < 0 && _varArg is { Count: > 0 })
        {
            JsonObject? raw = GetArgJson(_varArg.At(0));
            string? typeName = raw?["type"]?.GetValue<string>();
            string? source = raw?["sourceType"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(source) && !string.IsNullOrWhiteSpace(typeName) && source != typeName)
            {
                if (!await SchemaNode.Function.Reflect.Type.isassignableto(ctx, source, false, typeName)
                    && await SchemaNode.Function.Reflect.Type.isassignableto(ctx, source, true, typeName))
                    colIdx = _args.Count;
            }
        }

        if (colIdx == _preColIdx) return;

        for (int i = 0; i < _args.Count; i++)
        {
            StructNode node = _args[i];
            IValueAccess? source = node.GetAccessValue("source");
            bool selectable = colIdx < 0 || colIdx == i;

            if (defineNode is not null)
                source?.SetPropertyValue<EntrySource, FuncCall>(
                    selectable
                        ? FuncCallHelper.Build($"{NS_SYSTEM_SCHEMA_REFLECT_FUNC}.getaccessentries", "@args", $"@exps.{ARRAY_PREVIOUS}", true, NODE_SELF)
                        : null,
                    defineNode,
                    null);

            source?.SetPropertyValue<AccessEntryConsumer, FuncCall>(
                selectable
                    ? FuncCallHelper.Build($"{NS_SYSTEM_SCHEMA_REFLECT_TYPE}.isassignableto", NODE_SELF, true, $"@{ARRAY_ELEMENT}.type")
                    : null,
                node.Parent,
                null);
        }

        _preColIdx = colIdx;
        if (colIdx >= 0 && colIdx < _args.Count)
            _varArg?.SetCollectionSelectable(false);
        else if (colIdx < 0)
            _varArg?.SetCollectionSelectable(true);
    }

    private void ApplyFuncRelations(DataNode arg)
    {
        string? name = arg.Name?.ToLowerInvariant();
        if (string.IsNullOrEmpty(name) || _funcType is null) return;

        List<IRelation> relations = _funcType.GetRelations()
            .Where(r => r.Target.Equals(name, StringComparison.OrdinalIgnoreCase)
                        || r.Target.StartsWith($"{name}.", StringComparison.OrdinalIgnoreCase))
            .ToList<IRelation>();
        if (relations.Count == 0) return;

        RelationInfo info = new() { Owner = this };
        info.Relations.AddRange(relations);
        arg.AttachRelations([info]);
    }

    private IValueAccess? FindDefineNode()
    {
        IValueAccess? node = this;
        while (node is not null && !(node.Type?.Name.Equals($"{NS_SYSTEM_SCHEMA_FUNC}.schema", StringComparison.OrdinalIgnoreCase) ?? false))
            node = node.Parent;
        return node;
    }

    private static string GetArgName(FunctionNodeArgument arg)
        => (arg.GetProperty<Display>()?.Value ?? new LocaleString(arg.Name)).ToString();

    private void WriteBackRawValue() => OnNext(BuildJsonArray());

    private CallArg[] BuildCallArgs()
    {
        List<CallArg> result = [];
        foreach (StructNode arg in _args)
            result.Add(FuncCallHelper.ToCallArg(GetArgJson(arg)));

        if (_varArg is not null && _varArg.TryGetValue(typeof(CallArg[]), out object? varArgs) && varArgs is CallArg[] values)
            result.AddRange(values);

        return result.ToArray();
    }

    private JsonArray BuildJsonArray()
    {
        JsonArray array = [];
        foreach (StructNode arg in _args)
        {
            JsonObject? obj = GetArgJson(arg);
            array.Add(obj is null ? new JsonObject() : obj.DeepClone());
        }
        if (_varArg is not null)
            foreach (StructNode arg in _varArg)
            {
                JsonObject? obj = GetArgJson(arg);
                array.Add(obj is null ? new JsonObject() : obj.DeepClone());
            }
        return array;
    }

    private static JsonObject? GetArgJson(IValueAccess? node)
        => node is not null && node.TryGetValue<JsonObject>(out JsonObject? obj) ? obj : null;

    #endregion
}
