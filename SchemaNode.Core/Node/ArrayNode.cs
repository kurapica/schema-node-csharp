using System.Collections;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using SchemaNode.Runtime;
using ArrayType = SchemaNode.Runtime.ArrayType;
using StructFieldType = SchemaNode.Runtime.StructFieldType;
using StructType = SchemaNode.Runtime.StructType;
using ValueType = SchemaNode.Runtime.ValueType;
using static SchemaNode.Utility.Constant;
using System.Security.Cryptography.X509Certificates;

namespace SchemaNode.Node;

public class ArrayNode : DataNode, IEnumerable<DataNode>
{
    #region Constructors
    
    public ArrayNode(NodeType type)
    {
        switch (type)
        {
            case ArrayType arrayType:
                Type = arrayType;
                ElementType = arrayType.Element ?? throw new Exception($"The type '{type.Name}' is not a valid array type.");
                break;
            case ValueType valueType:
                Type = valueType.ArrayType ?? valueType;
                ElementType = valueType;
                break;
            default:
                throw new ArgumentException($"The node type '{type.Name}' is not a value type.", nameof(type));
        }
    }

    public ArrayNode(NodeType type, object value): this(type)
    {
        if (!TrySetValue(value))
            throw new InvalidCastException($"Invalid array value type '{value.GetType()}'.");
    }

    internal ArrayNode(ArrayNode array, int count)
    {
        Type = array.Type;
        ElementType = array.ElementType;
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
    public override bool TrySetValue<T>(T? value) where T : default
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
    public override DataNode? GetAccessValue(ReadOnlySpan<char> source)
    {
        if (source.SequenceEqual(NODE_SELF)) return this;
        if (source.SequenceEqual(ARRAY_PREVIOUS)) return new ArrayNode(this, this.Count - 1);

        var lastEle = _elements.LastOrDefault();
        if (source.SequenceEqual(ARRAY_ELEMENT)) return lastEle;
        return lastEle?.GetAccessValue(source);
    }

    /// <inheritdoc/>
    public override bool IsValid => _elements.All(element => element.IsValid);

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

    public IEnumerator<DataNode> GetEnumerator() => _elements.GetEnumerator();

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

        node = ElementType.Create();
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