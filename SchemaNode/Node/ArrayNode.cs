using SchemaNode.Runtime;
using SchemaNode.Utility;
using System.Collections;
using System.Data;
using System.Text.Json.Nodes;

namespace SchemaNode.Node;

public class ArrayNode : AnySchemaNode, IEnumerable<AnySchemaNode>
{
    public ArrayNode(AnySchemeType type, object? value = null) : base(type, null)
    {
        ElementType = type is ArrayType arr ? arr.ElementNode : type;
        Value = value;
    }

    public AnySchemeType? ElementType { get; set; }

    public object? this[int index]
    {
        get {
            if (ElementType != null) 
                return index >= 0 && index < _elements.Count ? _elements[index] : null;
            else
                return index >= 0 && index < _rawElements.Count ? _rawElements[index] : null;
        }
        set
        {
            if (index < 0) throw new IndexOutOfRangeException();

            if (ElementType != null)
            {
                if (index < _elements.Count)
                {
                    _elements[index].Value = value;
                }
                else if (index == _elements.Count)
                {
                    _elements.Add(ElementType.CreateNode(value) ?? throw new NotSupportedException());
                }
                else
                {
                    throw new IndexOutOfRangeException();
                }
            }
            else
            {
                if (index < _rawElements.Count)
                {
                    _rawElements[index] = value!;
                }
                else if (index == _rawElements.Count)
                {
                    _rawElements.Add(value!);
                }
                else
                {
                    throw new IndexOutOfRangeException();
                }
            }
        }
    }

    public int Count => ElementType != null ? _elements.Count : _rawElements.Count;

    public override bool IsEmpty => Count == 0;

    public override object? Value
    {
        get => this;
        set
        {
            if (ElementType != null)
            {
                if (value == null)
                {
                    _elements.Clear();
                }
                else if (value is IEnumerable<AnySchemaNode> nodes)
                {
                    _elements = nodes.Where(n => n.Type.CanBeUseAs(ElementType)).ToList();
                }
                else if (value is IEnumerable objs)
                {
                    _elements.Clear();
                    foreach (object o in objs)
                    {
                        _elements.Add(ElementType.CreateNode(o) ?? throw new NotSupportedException());
                    }
                }
                else
                {
                    Add(value);
                }
            }
            else
            {
                if (value == null)
                {
                    _rawElements.Clear();
                }
                else if (value is IEnumerable<AnySchemaNode> nodes)
                {
                    _rawElements = nodes.Select(p => (object)p).ToList();
                }
                else if (value is IEnumerable objs)
                {
                    _rawElements.Clear();
                    foreach (object o in objs)
                    {
                        _rawElements.Add(o);
                    }
                }
                else
                {
                    throw new InvalidCastException();
                }
            }
        }
    }

    public void AddRange(IEnumerable node)
    {
        if (ElementType != null)
        {
            foreach (var o in node)
            {
                _elements.Add(ElementType.CreateNode(o) ?? throw new NotSupportedException());
            }
        }
        else
        {
            foreach (var o in node)
            {
                _rawElements.Add(o);
            }
        }
    }

    public void Add(object node)
    {
        if (ElementType != null)
        {
            _elements.Add(ElementType.CreateNode(node) ?? throw new NotSupportedException());
        }
        else
        {
            _rawElements.Add(node);
        }
    }

    public override object? ToTypeValue(Type type)
    {
        if (type == typeof(ArrayNode))
            return this;

        if (ElementType != null)
        {
            if (type.IsArray)
            {
                var elementType = type.GetElementType() ?? typeof(object);
                var array = Array.CreateInstance(elementType, _elements.Count);
                for (int i = 0; i < _elements.Count; i++)
                {
                    array.SetValue(_elements[i].ToTypeValue(elementType) ?? DBNull.Value, i);
                }
                return array;
            }
            else if (type == typeof(IEnumerable))
            {
                return _elements.Select(e => e.Value ?? DBNull.Value);
            }
            else if (type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type))
            {
                var genericType = type.GetGenericArguments()[0];
                var listType = typeof(List<>).MakeGenericType(genericType);
                var list = (IList)Activator.CreateInstance(listType)!;
                foreach (var element in _elements)
                {
                    list.Add(element.ToTypeValue(genericType) ?? DBNull.Value);
                }
                return list;
            }
            else if (type == typeof(JsonArray) || type == typeof(JsonNode))
            {
                return ToJson();
            }
        }
        else
        {
            if (type.IsArray)
            {
                var elementType = type.GetElementType() ?? typeof(object);
                var array = Array.CreateInstance(elementType, _rawElements.Count);
                for (int i = 0; i < _rawElements.Count; i++)
                {
                    array.SetValue(elementType.TryConvert(_rawElements[i]), i);
                }
                return array;
            }
            else if (type == typeof(IEnumerable))
            {
                return _rawElements;
            }
            else if (type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type))
            {
                var genericType = type.GetGenericArguments()[0];
                var listType = typeof(List<>).MakeGenericType(genericType);
                var list = (IList)Activator.CreateInstance(listType)!;
                foreach (var element in _rawElements)
                {
                    list.Add(genericType.TryConvert(element));
                }
                return list;
            }
            else if (type == typeof(JsonArray) || type == typeof(JsonNode))
            {
                return ToJson();
            }
        }
        return null;
    }

    public override JsonArray? ToJson()
    {
        JsonArray array = new();
        foreach(var element in _elements)
        {
            array.Add(element.ToJson());
        }
        return array;
    }

    public IEnumerator<AnySchemaNode> GetEnumerator()
    {
        return _elements.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ElementType != null ? ((IEnumerable)_elements).GetEnumerator() : ((IEnumerable)_rawElements).GetEnumerator();
    }

    List<AnySchemaNode> _elements = [];
    List<object> _rawElements = [];
}
