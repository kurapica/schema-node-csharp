using SchemaNode.Context;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using System.Collections;
using System.Collections.Immutable;
using System.Reactive;
using System.Reactive.Subjects;
using System.Text.Json;
using System.Text.Json.Nodes;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
using ArrayType = SchemaNode.Runtime.ArrayType;
using StructFieldType = SchemaNode.Runtime.StructFieldType;
using StructType = SchemaNode.Runtime.StructType;
using ValueType = SchemaNode.Runtime.ValueType;

namespace SchemaNode.Node;

/// <summary>
/// The array data node
/// </summary>
public class ArrayNode : DataNode, IEnumerable<IValueAccess>
{
    #region Constructors

    public ArrayNode(ISchemaContext context, IValueTypeAccess type, object? value = null, IValueAccess? parent = null, params IPropertyProvider[] propertyProviders)
        : base(context, type, null, parent, propertyProviders)
    {
        Type = (type as ValueType)?.ArrayType ?? type;
        ElementType = (type is ArrayType arr ? arr.Element : type) ??
                      throw new Exception($"The type '{type.Name}' is not a valid array type.");

        if (value is not null)
        {
            if (!TrySetValue(value))
                throw new InvalidCastException($"Failed to set value to schema type {type.Name}.");
            if (FullMode)
                Confirm();
        }

        if (FullMode)
        {
            RelationInfo info = new() { Owner = this };
            info.Relations.AddRange(Type.GetRelations());
            AttachRelations([info]);
        }
    }

    #endregion

    #region Indexer

    /// <summary>
    /// array[index] access
    /// </summary>
    /// <param name="index"></param>
    /// <exception cref="IndexOutOfRangeException"></exception>
    /// <exception cref="InvalidCastException"></exception>
    public object? this[int index]
    {
        get => _elements.ElementAtOrDefault(index);
        set
        {
            if (index < 0) throw new IndexOutOfRangeException();

            if (index < _elements.Count)
            {
                if (!_elements[index].TrySetValue(value))
                    throw new InvalidCastException($"Invalid array element value type '{value?.GetType()}'.");
                return;
            }

            if (index == _elements.Count)
            {
                if (!TryCreateElement(value, out var node))
                    throw new InvalidCastException($"Invalid array element value type '{value?.GetType()}'.");
                _elements.Add(TrackElement(node));
                RefreshElementNames();
                return;
            }

            throw new IndexOutOfRangeException();
        }
    }

    #endregion

    #region Properties

    /// <summary>
    /// The array element type
    /// </summary>
    public IValueTypeAccess ElementType { get; }

    /// <summary>
    /// The element count
    /// </summary>
    public int Count => _elements.Count;

    #endregion

    #region Implementation

    /// <inheritdoc/>
    public override bool IsEmpty => Count == 0;

    /// <inheritdoc/>
    public override void ClearDisplayOnlyNode()
    {
        base.ClearDisplayOnlyNode();
        foreach (var item in _elements.OfType<DataNode>())
            item.ClearDisplayOnlyNode();
    }

    /// <inheritdoc/>
    public sealed override bool TrySetValue<T>(T? value) where T : default
    {
        if (value is ArrayNode arrayNode)
        {
            if (ReferenceEquals(arrayNode, this)) return true;
            return ReplaceTypedRange(arrayNode._elements.Cast<object?>());
        }

        if (value == null)
        {
            ClearValue();
            return true;
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Array } jsonElement)
            return ReplaceTypedRange(jsonElement.EnumerateArray().Select(static item => (object?)item));

        if (value is JsonArray jsonArray)
            return ReplaceTypedRange(jsonArray);

        if (value is not string && value is not JsonObject && value is IEnumerable enumerable)
            return ReplaceTypedRange(enumerable.Cast<object?>());

        // for single value
        ClearValue();
        Add(value);
        return true;
    }

    /// <inheritdoc/>
    public override bool TryGetValue(Type type, out object? value)
    {
        if (type == typeof(object) || type.IsAssignableFrom(typeof(ArrayNode)))
        {
            value = this;
            return true;
        }

        if (type == typeof(JsonArray) || type == typeof(JsonNode))
        {
            value = GetJsonArray();
            return true;
        }

        if (type == typeof(string))
        {
            value = GetJsonArray().ToJsonString();
            return true;
        }

        if (type == typeof(IEnumerable))
        {
            value = _elements.Select(static element => ToLiteralValue(element));
            return true;
        }

        if (type.IsArray)
        {
            Type itemType = type.GetElementType() ?? typeof(object);
            Array array = Array.CreateInstance(itemType, Count);
            for (int i = 0; i < Count; i++)
                array.SetValue(ToLiteralValue(_elements[i], itemType), i);
            value = array;
            return true;
        }

        if (type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type))
        {
            Type itemType = type.GetGenericArguments()[0];
            Type listType = typeof(List<>).MakeGenericType(itemType);
            IList list = (IList)Activator.CreateInstance(listType)!;
            foreach (var element in _elements)
                list.Add(ToLiteralValue(element, itemType));
            value = list;
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
    public override bool Changed => _elements.OfType<DataNode>().Any(e => e.Changed) || (_original?.Count ?? 0) != Count;

    /// <inheritdoc/>
    public override void Confirm()
    {
        foreach (var element in _elements.OfType<DataNode>())
            element.Confirm();
        _original = GetJsonArray();
    }

    /// <inheritdoc/>
    public override void Reset() => TrySetValue<object>(_original);

    /// <inheritdoc/>
    public override void ClearValue()
    {
        _elements.Clear();
        OnNext(GetJsonArray());
    }

    /// <summary>
    /// Gets the access value
    /// </summary>
    public override IValueAccess? GetAccessValue(string path, IValueAccess? node = null)
    {
        if (base.GetAccessValue(path, node) is { } v) return v;

        string[] paths = path.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
        int eleIndex = -1;
        IValueAccess? branch = node;

        // locate the node's branch
        while (branch is not null)
        {
            if ((eleIndex = _elements.FindIndex(e => e == branch)) >= 0) break;
            branch = branch.Parent;
        }

        // previous array
        if (paths[0].Equals(ARRAY_PREVIOUS, StringComparison.OrdinalIgnoreCase))
            return eleIndex >= 0
                ? new SliceArrayNode(Context, this, 0, eleIndex, _elements[eleIndex])
                : node is null ? this : null;
        if (!paths[0].Equals(ARRAY_ELEMENT, StringComparison.OrdinalIgnoreCase)) return null;

        // deep access
        var arrayEle = eleIndex >= 0 ? _elements[eleIndex] : null;
        return paths.Length <= 1 ? arrayEle : arrayEle?.GetAccessValue(paths[1], node);
    }

    /// <inheritdoc/>
    public override bool IsValid => _elements.All(element => element.IsValid) && base.IsValid;

    /// <inheritdoc/>
    public override JsonNode? Violated => !IsValid ? new JsonArray(_elements.Select(e => e.Violated ?? JsonValue.Create(true)).ToArray()) : null;

    /// <inheritdoc/>
    public override IEnumerable<DataNode> GetErrorNodes()
    {
        foreach (var element in _elements.OfType<DataNode>())
            foreach (var node in element.GetErrorNodes())
                yield return node;
        foreach (var node in base.GetErrorNodes())
            yield return node;
    }

    /// <inheritdoc/>
    public override IValueAccess Clone()
    {
        ArrayNode node = new(Context, ElementType, null, null, PropertyProviders.ToArray());
        foreach (var element in _elements)
            node.Add(element.Clone());
        node._original = _original?.DeepClone() as JsonArray;
        return node;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Add range items
    /// </summary>
    /// <param name="nodes"></param>
    public void AddRange(IEnumerable nodes)
    {
        foreach (object? node in nodes.Cast<object?>())
            Add(node);
        RefreshElementNames();
    }

    /// <summary>
    /// Add item
    /// </summary>
    public void Add(object? node)
    {
        if (node is null or IValueAccess { IsEmpty: true }) return;

        if (!TryCreateElement(node, out var element))
            throw new InvalidCastException($"Invalid array element value type '{node.GetType()}'.");
        _elements.Add(TrackElement(element));
    }

    /// <summary>
    /// Gets the element index
    /// </summary>
    public int IndexOf(IValueAccess node) => _elements.IndexOf(node);

    /// <summary>
    /// Clear elements without primary keys
    /// </summary>
    public ArrayNode FilterByPrimaryKeys(ImmutableList<string> primaryKeys)
    {
        if (ElementType is not StructType @struct) return this;

        StructFieldType[] fields = primaryKeys
            .Select(key => @struct.GetField(key))
            .OfType<StructFieldType>()
            .ToArray();

        if (fields.Length != primaryKeys.Count) return this;

        return new ArrayNode(Context, @struct)
        {
            _elements = _elements.Where(element =>
                    element is StructNode structNode &&
                    fields.All(field => structNode.GetAccessValue(field.Name) is { IsEmpty: false }))
                .ToList()
        };
    }

    public override bool Equals(IValueAccess? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is not ArrayNode otherArray) return false;
        if (Count != otherArray.Count) return false;

        if (!ElementType.IsAssignableTo(otherArray.ElementType)) return false;

        for (int i = 0; i < Count; i++)
        {
            object? left = this[i];
            object? right = otherArray[i];
            if (left is IValueAccess leftNode && right is IValueAccess rightNode)
            {
                if (!leftNode.Equals(rightNode)) return false;
            }
            else if (!Equals(left, right))
            {
                return false;
            }
        }

        return true;
    }

    public IEnumerator<IValueAccess> GetEnumerator() => _elements.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _elements.GetEnumerator();

    #endregion

    #region Dispose

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        _arrayDataOb?.Dispose();
        _arrayDataOb = null;

        foreach (var element in _elements)
            if (element is IDisposable disposable)
                disposable.Dispose();
        _elements.Clear();
        _relations = null;

        base.Dispose(disposing);
    }

    #endregion

    #region Subscription

    private Subject<(IValueAccess, object?, int)>? _arrayDataOb;

    /// <summary>
    /// Subscribe the array item change, normally for the element to observe the previous elements
    /// </summary>
    public IDisposable SubscribeItem(IObserver<(IValueAccess, object?, int)> observer, bool immediate = false)
    {
        _arrayDataOb ??= new Subject<(IValueAccess, object?, int)>();
        if (immediate) observer.OnNext((this, GetJsonArray(), Count));
        return _arrayDataOb.Subscribe(observer);
    }

    /// <summary>
    /// Subscribe the array item change with handler
    /// </summary>
    public IDisposable SubscribeItem(Action<(IValueAccess, object?, int)> handler, bool immediate = false)
        => SubscribeItem(Observer.Create(handler), immediate);

    /// <summary>
    /// Publish the array item change
    /// </summary>
    public void OnNextItem(int index)
    {
        if (!FullMode) return;
        _arrayDataOb?.OnNext((this, GetJsonArray(), index));
        OnNext(GetJsonArray());
    }

    #endregion

    #region Array Operations

    /// <summary>
    /// Add a new row to the array
    /// </summary>
    public IValueAccess? AddRow(int? index = null, object? data = null)
    {
        if (!TryCreateElement(data, out var element)) return null;
        if (element is not DataNode node) return null;

        int target = index ?? _elements.Count;
        _elements.Insert(target, node);

        if (FullMode)
        {
            node.ApplyPropertyEffects();
            if (_relations is not null) node.AttachRelations(_relations.ToArray());
            RecordSubscription(node.Subscribe(items => WriteBackRawValue(items.Item1, items.Item2)), this);
        }

        RefreshElementNames();
        if (target + 1 < _elements.Count)
        {
            for (int i = target + 1; i < _elements.Count; i++)
                if (_elements[i] is DataNode item)
                    WriteBackRawValue(item, null);
        }
        else
            OnNextItem(target);

        _ = InitElementAsync(node);
        return node;
    }

    /// <summary>
    /// Delete rows from the array
    /// </summary>
    public void DelRows(int start, int count = 1)
    {
        if (start < 0 || start >= _elements.Count || count < 1) return;
        _elements.RemoveRange(start, Math.Min(count, _elements.Count - start));
        RefreshElementNames();
        OnNextItem(start);
    }

    /// <summary>
    /// Move a row in the array
    /// </summary>
    public void MoveRow(int from, int to)
    {
        if (from == to || from < 0 || to < 0 || from >= _elements.Count || to >= _elements.Count) return;

        object? temp = _elements[from].GetValue<object>();
        object? tempOriginal = _elements[from].GetOriginal<object>();
        if (from < to)
        {
            for (int i = from; i < to; i++)
            {
                _elements[i].TrySetValue(_elements[i + 1].GetOriginal<object>());
                ConfirmAt(i);
                _elements[i].TrySetValue(_elements[i + 1].GetValue<object>());
            }
        }
        else
        {
            for (int i = from; i > to; i--)
            {
                _elements[i].TrySetValue(_elements[i - 1].GetOriginal<object>());
                ConfirmAt(i);
                _elements[i].TrySetValue(_elements[i - 1].GetValue<object>());
            }
        }
        _elements[to].TrySetValue(tempOriginal);
        ConfirmAt(to);
        _elements[to].TrySetValue(temp);
        OnNextItem(to);
    }

    #endregion

    #region Relation

    /// <inheritdoc/>
    public override void AttachRelations(IRelationInfo[] relations)
    {
        if (!FullMode) return;

        List<RelationInfo> elementRelations = [];
        foreach (var info in relations)
        {
            foreach (var relation in info.Relations)
            {
                string[] paths = relation.Target.Split('.', StringSplitOptions.RemoveEmptyEntries);
                IValueAccess? curr = info.Owner;
                int index = 0;

                // indicate this
                while (curr is not null && curr != this && index < paths.Length)
                    curr = curr.GetAccessValue(paths[index++], this);
                if (curr is null) continue;

                // set relation to this
                if (index == paths.Length)
                {
                    relation.Attach(info.Owner, this);
                    continue;
                }

                RelationInfo? exist = elementRelations.Find(f => f.Owner == info.Owner);
                if (exist is not null)
                    exist.Relations.Add(relation);
                else
                {
                    RelationInfo item = new() { Owner = info.Owner };
                    item.Relations.Add(relation);
                    elementRelations.Add(item);
                }
            }
        }

        foreach (var element in _elements)
            element.AttachRelations(elementRelations.ToArray());

        // merge relations, their owner should be different, just concat
        if (_relations is null)
            _relations = elementRelations;
        else
        {
            foreach (var relation in elementRelations.Where(e => !_relations.Any(f => f.Owner == e.Owner)))
                _relations.Add(relation);
        }
    }

    #endregion

    #region Utility

    private bool ReplaceTypedRange(IEnumerable<object?> values)
    {
        List<IValueAccess> nodes = [];
        foreach (object? item in values)
        {
            if (item == null) continue;
            if (item is IValueAccess { IsEmpty: true }) continue;
            if (!TryCreateElement(item, out var node)) return false;
            nodes.Add(TrackElement(node));
        }

        _elements = nodes;
        RefreshElementNames();
        return true;
    }

    private bool TryCreateElement(object? value, out IValueAccess node)
    {
        if (value is IValueAccess dataNode && dataNode.Type.IsAssignableTo(ElementType))
        {
            node = dataNode;
            return true;
        }

        node = ElementType.Create(Context, this);
        return node.TrySetValue(value);
    }

    /// <summary>
    /// Track the element in full mode, apply effects, relations and subscriptions
    /// </summary>
    private IValueAccess TrackElement(IValueAccess node)
    {
        if (!FullMode || node is not DataNode element) return node;
        element.ApplyPropertyEffects();
        if (_relations is not null) element.AttachRelations(_relations.ToArray());
        RecordSubscription(node.Subscribe(items => WriteBackRawValue(items.Item1, items.Item2)), this);
        return node;
    }

    private void WriteBackRawValue(IValueAccess element, object? value)
    {
        int index = _elements.IndexOf(element);
        if (index >= 0) OnNextItem(index);
    }

    private void ConfirmAt(int index)
    {
        if (_elements[index] is DataNode node) node.Confirm();
    }

    private void RefreshElementNames()
    {
        if (!FullMode) return;
        for (int i = 0; i < _elements.Count; i++)
            if (_elements[i] is DataNode node)
                node.SetPropertyValue<Name, string>($"[{i}]", this, null);
    }

    private static async Task InitElementAsync(DataNode node)
    {
        await Task.Delay(1);
        await node.InitAsync();
    }

    private JsonArray GetJsonArray()
    {
        JsonArray array = [];
        foreach (var element in _elements)
            if (element.TryGetValue(out JsonNode? jsonElement))
                array.Add(jsonElement!.DeepClone());
        return array;
    }

    private static object? ToLiteralValue(IValueAccess node, Type? targetType = null)
    {
        if (targetType != null && node.TryGetValue(targetType, out object? typedValue))
            return typedValue;

        return node.TryGetValue(out object? value) ? value : null;
    }

    private List<IValueAccess> _elements = [];
    private JsonArray? _original;
    private List<RelationInfo>? _relations;

    #endregion
}

/// <summary>
/// The array slice node, the read only view of the previous elements before the source node
/// </summary>
public class SliceArrayNode : DataNode
{
    private readonly ArrayNode _arrayNode;
    private readonly int _start;
    private readonly int _end;
    private readonly IValueAccess? _sourceNode;

    public SliceArrayNode(ISchemaContext context, ArrayNode arrayNode, int start = 0, int? end = null, IValueAccess? sourceNode = null)
        : base(context, arrayNode.Type, null, null)
    {
        _arrayNode = arrayNode;
        _start = start;
        _end = end ?? arrayNode.Count;
        _sourceNode = sourceNode;
    }

    /// <inheritdoc/>
    public override bool IsEmpty => _end <= _start || _arrayNode.Count == 0;

    /// <inheritdoc/>
    public override bool TrySetValue<T>(T? value) where T : default => false;

    /// <inheritdoc/>
    public override bool TryGetValue(Type type, out object? value)
    {
        JsonArray array = GetArray();
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
        if (!_arrayNode.TryGetOriginal(typeof(JsonArray), out object? original) || original is not JsonArray array)
        {
            value = null;
            return false;
        }
        JsonArray slice = new(array.Skip(_start).Take(_end - _start).Select(n => n?.DeepClone()).ToArray());
        value = type == typeof(string) ? slice.ToJsonString() : slice;
        return true;
    }

    /// <inheritdoc/>
    public override bool Changed => false;

    /// <inheritdoc/>
    public override void Confirm()
    {
    }

    /// <inheritdoc/>
    public override void Reset()
    {
    }

    /// <inheritdoc/>
    public override void ClearValue()
    {
    }

    /// <inheritdoc/>
    public override IValueAccess Clone() => new SliceArrayNode(Context, _arrayNode, _start, _end, _sourceNode);

    /// <inheritdoc/>
    public override IDisposable Subscribe(IObserver<(IValueAccess, object?)> observer, bool immediate = false)
    {
        if (_sourceNode is null) return _arrayNode.Subscribe(observer, immediate);
        return _arrayNode.SubscribeItem(items =>
        {
            (IValueAccess node, object? item, int index) = items;
            int sourceIndex = _arrayNode.IndexOf(_sourceNode);
            if (sourceIndex <= 0 || index >= sourceIndex) return;
            observer.OnNext((node, SliceValue(item, index + 1)));
        });
    }

    /// <inheritdoc/>
    public override void AttachRelations(IRelationInfo[] relations)
    {
        // the slice node is a read only view, no relation should be attached
    }

    /// <inheritdoc/>
    public override void RecordSubscription(IDisposable disposable, object? source) => _arrayNode.RecordSubscription(disposable, source);

    /// <inheritdoc/>
    public override void ClearSubscription(object? source) => _arrayNode.ClearSubscription(source);

    private JsonArray GetArray()
    {
        if (!_arrayNode.TryGetValue(out JsonArray? array) || array is null) return [];
        return new JsonArray(array.Skip(_start).Take(_end - _start).Select(n => n?.DeepClone()).ToArray());
    }

    private static object? SliceValue(object? item, int count)
    {
        if (item is not JsonArray array) return item;
        return new JsonArray(array.Take(count).Select(n => n?.DeepClone()).ToArray());
    }
}
