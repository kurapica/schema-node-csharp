using SchemaNode.Utility;
using System.Text.Json.Nodes;
using SchemaNode.Context;
using StructType = SchemaNode.Runtime.StructType;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;
using System.Diagnostics.CodeAnalysis;
using System.Collections;

namespace SchemaNode.Node;

public class StructNode : DataNode, IDictionary<string, object>
{
    private readonly DataNode[] _fields;
    private object? _csharpObject;

    #region Constructor

    public StructNode(StructType type)
    {
        Type = type;
        
        // init fields
        _fields = type.GetFields().Select(p => p.Type?.Create() ?? throw new Exception($"The struct {type.Name}'s field {p.Name} has not valid value type")).ToArray();
    }

    public StructNode(StructType type, object value) : this(type)
    {
        if (!TrySetValue(value))
            throw new InvalidCastException($"Invalid struct value type '{value.GetType()}'.");
    }

    #endregion

    #region Indexer

    // struct[field] access
    public object? this[string name]
    {
        get => GetAccessValue(name);
        set
        {
            DataNode? field = base.GetAccessValue(name);
            if (field == null) return;
            _csharpObject = null;
            field.TrySetValue(value);
        }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Gets or calc the field value
    /// </summary>
    /// <param name="context"></param>
    /// <param name="fieldName"></param>
    /// <returns></returns>
    public Task<DataNode?> GetFieldValueAsync(SchemaContext context, string fieldName)
        => (Type as StructType)!.GetFieldValueAsync(context, this, fieldName);

    /// <summary>
    /// Gets the struct field type by name
    /// </summary>
    public StructFieldType? GetFieldType(string fieldName) => (Type as StructType)?.GetField(fieldName);

    /// <summary>
    /// Gets the field type and values
    /// </summary>
    public IEnumerable<(StructFieldType field, DataNode value)> GetFields()
    {
        int i = 0;
        foreach (var field in (Type as StructType)!.GetFields())
        {
            yield return (field, _fields[i]);
            i++;
        }
    }
    
    internal void SetFieldValue(string fieldName, object? value)
    {
        DataNode? field = GetAccessValue(fieldName);
        if (field != null)
        {
            _csharpObject = null;
            field.TrySetValue(value);
        }
    }
    
    #endregion
    
    #region Implementation

    /// <summary>
    /// Gets the access value
    /// </summary>
    public override DataNode? GetAccessValue(ReadOnlySpan<char> source)
        => (source.IsEmpty || source.SequenceEqual(NODE_SELF)) 
            ? this 
            : _fields.ElementAtOrDefault((Type as StructType)?.GetIndex(source) ?? -1);

    /// <inheritdoc/>
    public override bool Equals(DataNode? other)
    {
        if (this == other) return true;
        if (other is not StructNode otherStruct || !Type.IsAssignableTo(otherStruct.Type)) return false;

        foreach (var (field, value) in GetFields())
        {
            if (field.DisplayOnly == true) continue;
            DataNode? otherValue = otherStruct.GetAccessValue(field.Name);
            if (otherValue == null || !value.Equals(otherValue)) return false;
        }
        return true;
    }

    /// <inheritdoc/>
    public override bool IsEmpty => _fields.All(f => f.IsEmpty);

    /// <inheritdoc/>
    public override bool IsValid => _fields.All(f => f.IsValid);

    public ICollection<string> Keys => throw new NotImplementedException();

    public ICollection<object> Values => throw new NotImplementedException();

    public int Count => throw new NotImplementedException();

    public bool IsReadOnly => throw new NotImplementedException();

    /// <inheritdoc/>
    public override bool TrySetValue<T>(T? value) where T : default
    {
        _csharpObject = null;
        if (value == null)
        {
            ClearValue();
        }
        else if (value is StructNode @struct)
        {
            if (@struct == this) return true;
            if (!@struct.Type.IsAssignableTo(Type)) return false;

            // copy
            foreach ((StructFieldType f, DataNode v) in GetFields())
            {
                DataNode? otherValue = @struct.GetAccessValue(f.Name);
                if (otherValue != null) v.TrySetValue(otherValue);
            }
        }
        else if (value is JsonObject obj)
        {
            Dictionary<string, DataNode> fieldMap = [];
            DataNode? unpackNode = null;
            foreach ((StructFieldType f, DataNode v)  in GetFields())
            {
                fieldMap.Add(f.Name, v);
                if (f.Unpack ?? false)
                    unpackNode = v;
            }

            JsonObject? packData = unpackNode != null ? new JsonObject() : null;
            foreach ((string key, JsonNode? val) in obj)
            {
                if (fieldMap.TryGetValue(key.ToLower(), out DataNode? field))
                {
                    if (!field.TrySetValue(val)) return false;
                }
                else if (packData != null)
                {
                    packData[key] = val?.DeepClone();
                }
            }

            if (unpackNode is { IsEmpty: true } && packData != null && !packData.IsEmpty())
                if (!unpackNode.TrySetValue(packData)) return false;
        }
        else if (value.GetType() == CsharpType)
        {
            foreach ((StructFieldType f, DataNode v) in GetFields())
                if (!v.TrySetValue(f.Property?.GetValue(value) ?? false)) return false;
        }
        else
        {
            throw new InvalidCastException($"Invalid value type {value.GetType()}");
        }
        return true;
    }

    /// <inheritdoc/>
    public override bool TryGetValue(Type type, out object? value)
    {
        if (type == typeof(JsonObject))
        {
            JsonObject result = [];
            foreach ((StructFieldType f, DataNode v) in GetFields())
            {
                if (v.IsEmpty) continue;
                if (f.Unpack ?? false)
                {
                    if (v.TryGetValue(out JsonObject? obj) && obj != null)
                        foreach ((string key, JsonNode? val) in obj)
                            result[key] = val?.DeepClone();
                }
                else if (v.TryGetValue(out JsonNode? d) && d != null && !d.IsEmpty())
                {
                    result.Add(f.Name, d.DeepClone());
                }
            }
            value = result;
            return true;
        }
        
        if (type.IsAssignableFrom(typeof(StructNode)))
        {
            value = this;
            return true;
        }
        
        if (CsharpType != null && CsharpType.IsAssignableTo(type))
        {
            if (_csharpObject != null)
            {
                value = _csharpObject;
            }
            else if (TryGetValue(out JsonObject? obj) && obj != null)
            {
                _csharpObject = obj.FromJson(CsharpType);
                value = _csharpObject;
                return _csharpObject != null;
            }
            value = null;
            return true;
        }

        // convert to string
        if (type == typeof(string) && TryGetValue(out JsonObject? jsonObj))
        {
            value = jsonObj?.ToJsonString();
            return true;
        }
        
        value = null;
        return false;
    }

    /// <inheritdoc/>
    public override void ClearValue()
    {
        foreach (DataNode field in _fields)
            field.ClearValue();
    }

    public void Add(string key, object value) => throw new NotSupportedException();

    public bool ContainsKey(string key) => (Type as StructType)?.GetField(key) != null;

    public bool Remove(string key) => throw new NotSupportedException();

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out object value)
    {
        var field = GetAccessValue(key);
        if (field != null && field.TryGetValue(out object? v) && v != null)
        {
            value = v;
            return true;
        }
        value = null;
        return false;
    }

    public void Add(KeyValuePair<string, object> item) => throw new NotSupportedException();

    public void Clear() => throw new NotSupportedException();

    public bool Contains(KeyValuePair<string, object> item)
    {
        throw new NotImplementedException();
    }

    public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex) => throw new NotSupportedException();

    public bool Remove(KeyValuePair<string, object> item) => throw new NotSupportedException();

    public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
    {
        foreach ((StructFieldType field, DataNode value) in GetFields())
        {
            if (value.IsEmpty) continue;
            if (value.TryGetValue(out object? v) && v != null)
                yield return new KeyValuePair<string, object>(field.Name, v);
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    #endregion
}
