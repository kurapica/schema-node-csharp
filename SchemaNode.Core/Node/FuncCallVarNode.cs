using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Property.Array;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Function;
using SchemaNode.Property.String;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Struct;
using SchemaNode.Utility;
using System.Collections;
using System.Reactive;
using System.Reactive.Subjects;
using System.Text.Json;
using System.Text.Json.Nodes;
using static SchemaNode.Utility.Constant;
using StructType = SchemaNode.Runtime.StructType;

namespace SchemaNode.Node;

/// <summary>
/// The function expression variable data node, the variadic argument of a function call
/// </summary>
public class FuncCallVarNode : DataNode, IEnumerable<StructNode>
{
    #region Fields

    private readonly List<StructNode> _args = [];
    private readonly StructType _argType;
    private Subject<(IValueAccess, int)>? _argCountOb;
    private bool _colSelectable;
    private JsonArray? _original;

    #endregion

    #region Constructor

    public FuncCallVarNode(ISchemaContext context, IValueTypeAccess type, object? value = null, IValueAccess? parent = null, params IPropertyProvider[] propertyProviders)
        : base(context, type, null, parent, propertyProviders)
    {
        _argType = type as StructType
            ?? throw new InvalidCastException($"The type '{type.Name}' is not a valid function call argument type.");

        if (value is not null && !TrySetValue(value))
            throw new InvalidCastException($"Failed to set value to schema type {type.Name}.");

        // subscribe the params list property
        if (FullMode)
            RecordSubscription(SubscribeProperty<ParamsList, Entry<string>>(_ => RefreshParamsList(), false), this);
    }

    #endregion

    #region Value Access

    /// <inheritdoc/>
    public override bool IsEmpty => _args.Count == 0
        || _args.All(e => (e.GetAccessValue("value")?.IsEmpty ?? true) && (e.GetAccessValue("source")?.IsEmpty ?? true));

    /// <inheritdoc/>
    public override bool TrySetValue<T>(T? value) where T : default
    {
        if (value is FuncCallVarNode other)
        {
            if (ReferenceEquals(other, this)) return true;
            ClearValue();
            foreach (StructNode node in other._args)
                AddRow(node.Clone());
            return true;
        }

        if (value is null)
        {
            ClearValue();
            return true;
        }

        JsonArray? data = FuncCallHelper.ToJsonArray(value);
        if (data is null) return false;

        ClearValue();
        foreach (JsonNode? item in data)
        {
            if (item is null) continue;
            AddRow(item);
        }
        OnNext(this);
        return true;
    }

    /// <inheritdoc/>
    public override bool TryGetValue(Type type, out object? value)
    {
        if (type == typeof(CallArg[]) || type.IsArray && type.GetElementType() == typeof(CallArg))
        {
            value = BuildCallArgs(true);
            return true;
        }

        JsonArray array = BuildJsonArray();
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
    public override bool Changed => _args.Any(e => e.Changed);

    /// <inheritdoc/>
    public override void Confirm()
    {
        foreach (StructNode node in _args)
            node.Confirm();
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
        _args.Clear();
        OnNext(this);
    }

    /// <inheritdoc/>
    public override IValueAccess Clone()
    {
        FuncCallVarNode clone = new(Context, _argType, null, null, PropertyProviders.ToArray());
        foreach (StructNode node in _args)
            clone._args.Add((StructNode)node.Clone());
        clone._original = _original?.DeepClone() as JsonArray;
        return clone;
    }

    /// <inheritdoc/>
    public override bool IsValid => _args.All(e => e.IsValid) && base.IsValid;

    /// <inheritdoc/>
    public override JsonNode? Violated
        => !IsValid ? new JsonArray(_args.Select(e => e.Violated ?? JsonValue.Create(true)).ToArray()) : null;

    /// <inheritdoc/>
    public override IEnumerable<DataNode> GetErrorNodes()
    {
        foreach (StructNode node in _args)
            foreach (DataNode error in node.GetErrorNodes())
                yield return error;
        foreach (DataNode error in base.GetErrorNodes())
            yield return error;
    }

    #endregion

    #region Path Navigation

    /// <inheritdoc/>
    public override IValueAccess? GetAccessValue(string path, IValueAccess? node = null)
    {
        if (base.GetAccessValue(path, node) is { } v) return v;
        if (string.IsNullOrEmpty(path)) return null;

        string[] paths = path.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
        if (!paths[0].Equals(ARRAY_ELEMENT, StringComparison.OrdinalIgnoreCase)) return null;

        IValueAccess? item = node;
        while (item is not null && item.Parent != this)
            item = item.Parent;
        return paths.Length <= 1 ? item : item?.GetAccessValue(paths[1], node);
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

        base.Dispose(disposing);
    }

    #endregion

    #region Args Changed

    /// <summary>
    /// Subscribe the argument count change, normally for the parent function call
    /// </summary>
    public IDisposable SubscribeArgs(Action<(IValueAccess, int)> handler, bool immediate = false)
    {
        _argCountOb ??= new Subject<(IValueAccess, int)>();
        if (immediate) handler((this, _args.Count));
        return _argCountOb.Subscribe(items => handler((items.Item1, items.Item2)));
    }

    /// <summary>
    /// Publish the argument count change
    /// </summary>
    public void OnNextArgs()
    {
        if (!FullMode) return;
        _argCountOb?.OnNext((this, _args.Count));
    }

    #endregion

    #region Iterable

    /// <summary>
    /// Gets the argument count
    /// </summary>
    public int Count => _args.Count;

    /// <inheritdoc/>
    public IEnumerator<StructNode> GetEnumerator() => _args.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _args.GetEnumerator();

    /// <summary>
    /// Gets the argument at the given index or name
    /// </summary>
    public StructNode? At(int index) => index >= 0 && index < _args.Count ? _args[index] : null;

    /// <summary>
    /// Gets the argument by name
    /// </summary>
    public StructNode? At(string name)
        => _args.FirstOrDefault(e => e.Name?.Equals(name, StringComparison.OrdinalIgnoreCase) == true);

    #endregion

    #region Methods

    /// <summary>
    /// Sets the collection selectable for the first argument source
    /// </summary>
    public void SetCollectionSelectable(bool selectable)
    {
        _colSelectable = selectable;
        StructNode? node = _args.FirstOrDefault();
        if (node is null) return;

        node.GetAccessValue("source")?.SetPropertyValue<AccessEntryConsumer, FuncCall>(
            selectable
                ? FuncCallHelper.Build($"{NS_SYSTEM_SCHEMA_REFLECT_TYPE}.isassignableto", NODE_SELF, true, $"@{ARRAY_ELEMENT}.type")
                : null,
            node.Parent,
            null);
    }

    /// <summary>
    /// Gets the arguments as call arguments
    /// </summary>
    internal CallArg[] BuildCallArgs(bool trim)
    {
        List<CallArg> result = [];
        foreach (StructNode node in _args)
            result.Add(FuncCallHelper.ToCallArg(GetArgJson(node)));

        if (trim)
        {
            while (result.Count > 0 && FuncCallHelper.IsEmptyArg(result[^1]))
                result.RemoveAt(result.Count - 1);
        }
        return result.ToArray();
    }

    /// <summary>
    /// Gets the arguments as json array
    /// </summary>
    internal JsonArray BuildJsonArray()
    {
        JsonArray array = [];
        foreach (StructNode node in _args)
        {
            JsonObject? obj = GetArgJson(node);
            array.Add(obj is null ? new JsonObject() : obj.DeepClone());
        }
        return array;
    }

    #endregion

    #region Utility

    /// <summary>
    /// Adds a new argument row
    /// </summary>
    public StructNode? AddRow(object? data, string? name = null)
    {
        if (_argType.Create(Context, null, this) is not StructNode node) return null;
        if (data is not null && !node.TrySetValue(data)) return null;

        _args.Add(node);
        if (!string.IsNullOrWhiteSpace(name))
            node.SetPropertyValue<Name, string>(name, this, null);

        if (FullMode)
        {
            node.ApplyPropertyEffects();
            node.RecordSubscription(node.Subscribe(_item => { _ = RefreshArgTypesAsync(); }), node);
            if (_args.Count == 1 && _colSelectable)
                SetCollectionSelectable(true);
        }
        return node;
    }

    private void RefreshParamsList()
    {
        if (!FullMode) return;

        Entry<string>? list = GetPropertyValue<ParamsList, Entry<string>>();
        Entry<string>[] entries = list is null
            ? []
            : list.Children is { Length: > 0 } ? list.Children
            : !string.IsNullOrWhiteSpace(list.Value) ? [list] : [];
        if (entries.Length == 0) return;

        while (_args.Count > entries.Length)
            _args.RemoveAt(_args.Count - 1);

        for (int i = 0; i < entries.Length; i++)
        {
            Entry<string> entry = entries[i];
            string display = entry.GetProperty<Display>()?.Value?.ToString() ?? entry.Value;
            if (i < _args.Count)
            {
                _args[i].SetPropertyValue<Name, string>(display, this, null);
                _args[i].GetAccessValue("type")?.TrySetValue(entry.Value);
            }
            else
                AddRow(new JsonObject { ["type"] = entry.Value }, display);
        }
        OnNextArgs();
    }

    private async Task RefreshArgTypesAsync()
    {
        if (!FullMode || GetProperty<ParamsList>()?.HasValue == true) return;

        OnNext(this);
        if (GetPropertyValue<ReadOnly, bool>()) return;

        int argCount = _args.Count;
        JsonObject? last = GetArgJson(_args.LastOrDefault());
        if (last is not null && (!IsEmptyNode(last["source"]) || !IsEmptyNode(last["value"])))
        {
            long max = GetPropertyValue<MaxSize, long>();
            if (max > 0 && _args.Count >= max) return;

            JsonObject data = new();
            if (last["type"] is { } type) data["type"] = type.DeepClone();
            AddRow(data);
        }
        else
        {
            long min = Math.Max(1L, GetPropertyValue<MinSize, long>());
            while (_args.Count > min)
            {
                JsonObject? prev = GetArgJson(_args[^2]);
                if (prev is null || !IsEmptyNode(prev["source"]) || !IsEmptyNode(prev["value"])) break;
                _args.RemoveAt(_args.Count - 1);
            }
        }

        if (_args.Count != argCount)
            OnNextArgs();
    }

    private static JsonObject? GetArgJson(IValueAccess? node)
        => node is not null && node.TryGetValue<JsonObject>(out JsonObject? obj) ? obj : null;

    private static bool IsEmptyNode(JsonNode? node) => node is null || node.IsEmpty();

    #endregion
}

/// <summary>
/// The function call helper, used to build the func call properties
/// </summary>
internal static class FuncCallHelper
{
    /// <summary>
    /// Builds a function call
    /// </summary>
    public static FuncCall Build(string func, params object?[] args)
        => new()
        {
            Mode = ApplyMode.Call,
            Func = func,
            Args = args.Select(ToCallArg).ToArray()
        };

    /// <summary>
    /// Converts an object to a call argument
    /// </summary>
    public static CallArg ToCallArg(object? value)
    {
        switch (value)
        {
            case null:
                return new CallArg();
            case CallArg arg:
                return arg;
            case string str when str.StartsWith("@@"):
                return new CallArg { Value = JsonValue.Create(str[1..]) };
            case string str when str.StartsWith('@'):
                return new CallArg { Source = str[1..].ToCamelCase() };
            case string str when str.StartsWith("$$"):
                return new CallArg { Value = JsonValue.Create(str[1..]) };
            case string str when str.StartsWith('$'):
                return new CallArg { Source = str };
            default:
                return new CallArg { Value = value is JsonNode node ? node.DeepClone() : JsonSerializer.SerializeToNode(value) };
        }
    }

    /// <summary>
    /// Converts a call argument json object to the call argument
    /// </summary>
    public static CallArg ToCallArg(JsonObject? obj)
    {
        if (obj is null) return new CallArg();
        return new CallArg
        {
            Type = obj["type"]?.GetValue<string>(),
            Source = obj["source"]?.GetValue<string>(),
            SourceType = obj["sourceType"]?.GetValue<string>(),
            Value = obj["value"]?.DeepClone()
        };
    }

    /// <summary>
    /// Converts a value to the json array, returns null if the value is not array like
    /// </summary>
    public static JsonArray? ToJsonArray(object? value)
    {
        switch (value)
        {
            case null:
            case string:
                return null;
            case JsonArray array:
                return (JsonArray)array.DeepClone();
            case JsonElement { ValueKind: JsonValueKind.Array } element:
                return JsonNode.Parse(element.GetRawText()) as JsonArray;
            case not IEnumerable:
                return null;
            default:
            {
                JsonArray result = [];
                foreach (object? item in (IEnumerable)value)
                {
                    if (item is null) continue;
                    JsonNode? node = item is JsonNode json ? json.DeepClone() : JsonSerializer.SerializeToNode(item);
                    if (node is not null) result.Add(node);
                }
                return result;
            }
        }
    }

    /// <summary>
    /// Whether the call argument is empty
    /// </summary>
    public static bool IsEmptyArg(CallArg arg)
        => string.IsNullOrWhiteSpace(arg.Source) && (arg.Value is null || arg.Value.IsEmpty());
}
