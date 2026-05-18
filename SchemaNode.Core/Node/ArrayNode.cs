using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
using SchemaNode.Runtime;
using SchemaNode.Utility;
using RuntimeArrayType = SchemaNode.Runtime.ArrayType;
using RuntimeStructFieldType = SchemaNode.Runtime.StructFieldType;
using RuntimeStructType = SchemaNode.Runtime.StructType;
using RuntimeValueType = SchemaNode.Runtime.ValueType;

namespace SchemaNode.Node;

public class ArrayNode : DataNode, IEnumerable<DataNode>
{
    public ArrayNode(NodeType type, object? value = null)
    {
        switch (type)
        {
            case RuntimeArrayType arrayType:
                Type = arrayType;
                ElementType = arrayType.Element;
                break;
            case RuntimeValueType valueType:
                Type = valueType.ArrayType ?? valueType;
                ElementType = valueType;
                break;
            default:
                throw new ArgumentException($"The node type '{type.Name}' is not a value type.", nameof(type));
        }

        if (value != null)
            SetValueInternal(value);
    }

    public RuntimeValueType? ElementType { get; }

    public object? this[int index]
    {
        get
        {
            if (index < 0) return null;
            return ElementType != null
                ? index < _elements.Count ? _elements[index] : null
                : index < _rawElements.Count ? _rawElements[index] : null;
        }
        set
        {
            if (index < 0) throw new IndexOutOfRangeException();

            if (ElementType != null)
            {
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
            }
            else
            {
                if (index < _rawElements.Count)
                {
                    _rawElements[index] = value;
                    return;
                }

                if (index == _rawElements.Count)
                {
                    _rawElements.Add(value);
                    return;
                }
            }

            throw new IndexOutOfRangeException();
        }
    }

    public int Count => ElementType != null ? _elements.Count : _rawElements.Count;

    public override bool IsEmpty => Count == 0;

    public override bool TrySetValue<T>(T? value) where T : default => SetValueInternal(value);

    public override bool TryGetValue(Type type, out object? value)
    {
        if (type == typeof(object) || type.IsAssignableFrom(typeof(ArrayNode)))
        {
            value = this;
            return true;
        }

        if (type == typeof(JsonArray) || type == typeof(JsonNode))
        {
            value = ToJson();
            return true;
        }

        if (type == typeof(IEnumerable))
        {
            value = ElementType != null
                ? _elements.Select(static element => ToLiteralValue(element)).ToList()
                : _rawElements;
            return true;
        }

        if (type.IsArray)
        {
            Type itemType = type.GetElementType() ?? typeof(object);
            Array array = Array.CreateInstance(itemType, Count);
            for (int i = 0; i < Count; i++)
            {
                object? item = ElementType != null
                    ? ToLiteralValue(_elements[i], itemType)
                    : ConvertRawValue(_rawElements[i], itemType);
                array.SetValue(item, i);
            }
            value = array;
            return true;
        }

        if (type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type))
        {
            Type itemType = type.GetGenericArguments()[0];
            Type listType = typeof(List<>).MakeGenericType(itemType);
            IList list = (IList)Activator.CreateInstance(listType)!;
            if (ElementType != null)
            {
                foreach (DataNode element in _elements)
                    list.Add(ToLiteralValue(element, itemType));
            }
            else
            {
                foreach (object? element in _rawElements)
                    list.Add(ConvertRawValue(element, itemType));
            }
            value = list;
            return true;
        }

        value = null;
        return false;
    }

    public override void ClearValue()
    {
        _elements.Clear();
        _rawElements.Clear();
    }

    public void AddRange(IEnumerable nodes)
    {
        foreach (object? node in nodes.Cast<object?>())
            Add(node);
    }

    public void Add(object? node)
    {
        if (node == null) return;
        if (node is DataNode { IsEmpty: true }) return;

        if (ElementType != null)
        {
            if (!TryCreateElement(node, out DataNode element))
                throw new InvalidCastException($"Invalid array element value type '{node.GetType()}'.");
            _elements.Add(element);
            return;
        }

        _rawElements.Add(node);
    }

    /// <summary>
    /// Clear elements without primary keys
    /// </summary>
    internal ArrayNode FilterByPrimaryKeys(string[] primaryKeys)
    {
        if (ElementType is not RuntimeStructType @struct) return this;

        RuntimeStructFieldType[] fields = primaryKeys
            .Select(key => @struct.GetField(key))
            .OfType<RuntimeStructFieldType>()
            .ToArray();

        if (fields.Length != primaryKeys.Length) return this;

        return new ArrayNode(@struct)
        {
            _elements = _elements.Where(element =>
                    element is StructNode structNode &&
                    fields.All(field => structNode.GetAccessValue(field.Name) is { IsEmpty: false }))
                .ToList()
        };
    }

    public object? ToTypeValue(Type type)
        => TryGetValue(type, out object? value) ? value : null;

    /// <summary>
    /// Gets the value with paths
    /// </summary>
    public DataNode? GetValueByPaths(string paths)
    {
        if (string.IsNullOrWhiteSpace(paths) || ElementType == null)
            return this;

        RuntimeValueType? type = ElementType;
        foreach (string path in paths.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (type is not RuntimeStructType @struct) return null;
            type = @struct.GetField(path)?.Type;
        }

        if (type == null) return null;

        ArrayNode result = new(type);
        foreach (DataNode element in _elements)
        {
            DataNode? field = element.GetAccessValue(paths);
            if (field != null)
                result.Add(field);
        }
        return result;
    }

    public JsonArray ToJson()
    {
        JsonArray array = [];

        if (ElementType != null)
        {
            foreach (DataNode element in _elements)
                array.Add(ToJsonNode(element));
        }
        else
        {
            foreach (object? raw in _rawElements)
                array.Add(ToJsonNode(raw));
        }

        return array;
    }

    public override string ToString() => ToJson().ToJsonString();

    public override bool Equals(DataNode? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is not ArrayNode otherArray) return false;
        if (Count != otherArray.Count) return false;

        if (ElementType != null && otherArray.ElementType != null && !ElementType.IsAssignableTo(otherArray.ElementType))
            return false;

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

    IEnumerator IEnumerable.GetEnumerator()
        => ElementType != null ? _elements.GetEnumerator() : _rawElements.GetEnumerator();

    private bool SetValueInternal(object? value)
    {
        if (value is ArrayNode arrayNode)
        {
            if (ReferenceEquals(arrayNode, this)) return true;
            return ReplaceEnumerable(arrayNode.ElementType != null
                ? arrayNode._elements.Cast<object?>()
                : arrayNode._rawElements);
        }

        if (value == null)
        {
            ClearValue();
            return true;
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Array } jsonElement)
            return ReplaceEnumerable(jsonElement.EnumerateArray().Select(static item => (object?)item));

        if (value is JsonArray jsonArray)
            return ReplaceEnumerable(jsonArray);

        if (value is not string && value is not JsonObject && value is IEnumerable enumerable)
            return ReplaceEnumerable(enumerable.Cast<object?>());

        ClearValue();
        Add(value);
        return true;
    }

    private bool ReplaceEnumerable(IEnumerable<object?> values)
        => ElementType != null ? ReplaceTypedRange(values) : ReplaceRawRange(values);

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

        _rawElements.Clear();
        _elements = nodes;
        return true;
    }

    private bool ReplaceRawRange(IEnumerable<object?> values)
    {
        _elements.Clear();
        _rawElements = values.Where(static item => item != null).ToList();
        return true;
    }

    private bool TryCreateElement(object? value, out DataNode node)
    {
        if (ElementType == null)
        {
            node = null!;
            return false;
        }

        if (value is DataNode dataNode && dataNode.Type.IsAssignableTo(ElementType))
        {
            node = dataNode;
            return true;
        }

        node = ElementType.Create();
        return node.TrySetValue(value);
    }

    private static JsonNode? ToJsonNode(object? value)
    {
        if (value is DataNode node)
        {
            if (node.TryGetValue(typeof(JsonNode), out object? json) && json is JsonNode jsonNode)
                return jsonNode.DeepClone();
            return ToLiteralValue(node)?.ToJsonNode(noError: true);
        }

        return value.ToJsonNode(noError: true);
    }

    private static object? ToLiteralValue(DataNode node, Type? targetType = null)
    {
        if (targetType != null && node.TryGetValue(targetType, out object? typedValue))
            return typedValue;

        return node.TryGetValue(out object? value) ? value : null;
    }

    private static object? ConvertRawValue(object? value, Type type)
        => type.TryConvert(value, out object? converted) ? converted : null;

    private List<DataNode> _elements = [];
    private List<object?> _rawElements = [];
}
