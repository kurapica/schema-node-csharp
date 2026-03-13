using SchemaNode.Runtime;
using SchemaNode.Utility;
using System.Collections;
using System.Text.Json.Nodes;
using SchemaNode.Context;
using SchemaNode.Schema;

namespace SchemaNode.Node;

public class ArrayTypeNode : AnySchemaNode, IEnumerable<AnySchemaNode>
{
    public ArrayTypeNode(AnySchemaType type, object? value = null) : base(SchemaContext.GetArraySchemaType(type)!)
    {
        ElementType = type is ArrayType arr ? arr.ElementSchemaType : type;
        Value = value;
    }

    public AnySchemaType? ElementType { get; set; }

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
                    _elements = nodes.Where(n => n.SchemaType.CanBeUseAs(ElementType)).ToList();
                }
                else if (value is not string && value is not JsonObject && value is IEnumerable objs)
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
                if (o is null) continue;
                if (o is AnySchemaNode { IsEmpty: true }) continue;
                _elements.Add(ElementType.CreateNode(o) ?? throw new NotSupportedException());
            }
        }
        else
        {
            foreach (var o in node)
            {
                if (o is null) continue;
                _rawElements.Add(o);
            }
        }
    }

    public void Add(object node)
    {
        if (node is null) return;
        if (node is AnySchemaNode { IsEmpty: true }) return;
        if (ElementType != null)
        {
            _elements.Add(ElementType.CreateNode(node) ?? throw new NotSupportedException());
        }
        else
        {
            _rawElements.Add(node);
        }
    }

    /// <summary>
    /// Clear elements without primary keys
    /// </summary>
    internal ArrayTypeNode FilterByPrimaryKeys(string[] primaryKeys)
    {
        if (ElementType is not StructType @struct || primaryKeys.Any(k => @struct.GetField(k) == null)) return this;
        StructFieldSchema[] fields = primaryKeys.Select(k => @struct.GetField(k)!).ToArray();        
        return new ArrayTypeNode(@struct)
        {
            _elements = _elements.Where(e =>
            {
                return e is StructTypeNode structNode && 
                       fields.Select(field => structNode.GetField(field.Name))
                           .All(value => value is not null && !value.IsEmpty);
            }).ToList()
        };
    }

    public override object? ToTypeValue(Type type)
    {
        if (type == typeof(ArrayTypeNode))
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

    /// <summary>
    /// Gets the value with paths
    /// </summary>
    public AnySchemaNode? GetValueByPaths(string paths) {
        if (ElementType != null)
        {
            AnySchemaType? type = ElementType;
            foreach (string path in paths.Split('.', StringSplitOptions.RemoveEmptyEntries))
            {
                if (type is not StructType @struct) return null;
                type = @struct.Fields.FirstOrDefault(f => f.Name.Equals(path, StringComparison.OrdinalIgnoreCase))
                    ?.SchemeType;
            }

            if (type == null) return null;
            AnySchemaNode result = new ArrayTypeNode(type);
            result.Value = _elements.Select(p => ((StructTypeNode)p).GetValueByPaths(paths)).Where(p => p != null).ToList();
            return result;
        }
        else
        {
            return this;
        }
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

    public override string ToString() => ToJson()?.ToString() ?? string.Empty;

    /// <summary>
    /// Equals the other node
    /// </summary>
    public override bool Equals(AnySchemaNode other)
    {
        if (this == other) return true;
        if (other is not ArrayTypeNode otherArray) return false;
        if (ElementType != otherArray.ElementType) return false;
        if (Count != otherArray.Count) return false;
        for (int i = 0; i < Count; i++)
        {
            if (!this[i]!.Equals(otherArray[i])) return false;
        }
        return true;
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
