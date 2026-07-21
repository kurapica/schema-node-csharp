using System.Collections;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using SchemaNode.Runtime;
using SchemaNode.Utility;
using ArrayType = SchemaNode.Runtime.ArrayType;
using StructFieldType = SchemaNode.Runtime.StructFieldType;
using StructType = SchemaNode.Runtime.StructType;
using ValueType = SchemaNode.Runtime.ValueType;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Node;

public class ArrayNode : DataNode, IEnumerable<IValueAccess>
{
    #region Constructors
    
    public ArrayNode(ValueType type, IValueAccess? parent = null)
    {
        Type = type.ArrayType ?? type;
        Parent = parent;
        ElementType = (type is ArrayType arr ? arr.Element : type) ??
                      throw new Exception($"The type '{type.Name}' is not a valid array type.");
    }

    public ArrayNode(ValueType type, object value, IValueAccess? parent = null): this(type, parent)
    {
        if (!TrySetValue(value))
            throw new InvalidCastException($"Failed to set value to schema type {type.Name}.");
    }

    private ArrayNode(ArrayNode array, int count): this(array.Type, array.Parent)
    {
        // only used for relation calc, no parent change
        _elements = array._elements.Take(count).ToList();
    }

    #endregion

    #region Indexer
    
    /// <summary>
    /// array[index] accesser
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
                if (!TryCreateElement(value, out DataNode node))
                    throw new InvalidCastException($"Invalid array element value type '{value?.GetType()}'.");
                _elements.Add(node);
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
    public ValueType ElementType { get; }
    
    /// <summary>
    /// The element count
    /// </summary>
    public int Count => _elements.Count;

    #endregion
    
    #region Implementation
    
    /// <inheritdoc/>
    public override bool IsEmpty => Count == 0;

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
            JsonArray array = [];
            foreach (DataNode element in _elements)
                if (element.TryGetValue(out JsonNode? jsonElement))
                    array.Add(jsonElement!.DeepClone());
            value = array;
            return true;
        }

        if (type == typeof(string))
        {
            if (TryGetValue(out JsonArray? jsonArray))
            {
                value = jsonArray?.ToJsonString();
                return true;
            }
            value = null;
            return false;
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
            foreach (DataNode element in _elements)
                list.Add(ToLiteralValue(element, itemType));
            value = list;
            return true;
        }

        value = null;
        return false;
    }

    /// <inheritdoc/>
    public override void ClearValue() => _elements.Clear();

    /// <inheritdoc/>
    public override IValueAccess? GetAccessValue(ReadOnlySpan<char> source, IValueAccess? node = null)
    {
        if (source.SeqEquals(NODE_SELF, StringComparison.OrdinalIgnoreCase)) return this;
        
        int eleIndex = -1;
        IValueAccess? branch = node;
        
        // locate the node's branch
        while (branch is not null)
        {
            if ((eleIndex = _elements.FindIndex(e => e == branch)) >= 0) break;
            branch = branch.Parent;
        }
        
        // previous array
        if (source.SeqEquals(ARRAY_PREVIOUS, StringComparison.OrdinalIgnoreCase)) 
            return eleIndex >= 0 ? new ArrayNode(this, eleIndex) : node is null ? this : null;

        // deep access
        DataNode? arrayEle = eleIndex >= 0 ? _elements[eleIndex] : node is null ? _elements.LastOrDefault() : null;
        return source.SeqEquals(ARRAY_ELEMENT, StringComparison.OrdinalIgnoreCase) ? arrayEle : arrayEle?.GetAccessValue(source);
    }

    /// <inheritdoc/>
    public override bool IsValid => _elements.All(element => element.IsValid);

    /// <inheritdoc/>
    public override DataNode Clone()
    {
        ArrayNode node = new ArrayNode(ElementType);
        foreach (DataNode element in _elements)
            node.Add(element.Clone());
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
    }

    /// <summary>
    /// Add item
    /// </summary>
    public void Add(object? node)
    {
        if (node is null or DataNode { IsEmpty: true }) return;

        if (!TryCreateElement(node, out DataNode element))
            throw new InvalidCastException($"Invalid array element value type '{node.GetType()}'.");
        _elements.Add(element);
    }

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

        return new ArrayNode(@struct)
        {
            _elements = _elements.Where(element =>
                    element is StructNode structNode &&
                    fields.All(field => structNode.GetAccessValue(field.Name) is { IsEmpty: false }))
                .ToList()
        };
    }

    public override bool Equals(DataNode? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is not ArrayNode otherArray) return false;
        if (Count != otherArray.Count) return false;

        if (!ElementType.IsAssignableTo(otherArray.ElementType)) return false;

        for (int i = 0; i < Count; i++)
        {
            object? left = this[i];
            object? right = otherArray[i];
            if (left is DataNode leftNode && right is DataNode rightNode)
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

    #region Utility
    
    private bool ReplaceTypedRange(IEnumerable<object?> values)
    {
        List<DataNode> nodes = [];
        foreach (object? item in values)
        {
            if (item == null) continue;
            if (item is DataNode { IsEmpty: true }) continue;
            if (!TryCreateElement(item, out DataNode node)) return false;
            nodes.Add(node);
        }

        _elements = nodes;
        return true;
    }

    private bool TryCreateElement(object? value, out DataNode node)
    {
        if (value is DataNode dataNode && dataNode.Type.IsAssignableTo(ElementType))
        {
            node = dataNode;
            return true;
        }

        node = ElementType.Create(this);
        return node.TrySetValue(value);
    }

    private static object? ToLiteralValue(DataNode node, Type? targetType = null)
    {
        if (targetType != null && node.TryGetValue(targetType, out object? typedValue))
            return typedValue;

        return node.TryGetValue(out object? value) ? value : null;
    }

    private List<DataNode> _elements = [];
    
    #endregion
}