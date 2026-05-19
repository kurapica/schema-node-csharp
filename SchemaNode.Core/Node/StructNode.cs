using SchemaNode.Utility;
using System.Text.Json.Nodes;
using SchemaNode.Context;
using StructType = SchemaNode.Runtime.StructType;
using SchemaNode.Runtime;

namespace SchemaNode.Node;

public class StructNode : DataNode
{
    private readonly DataNode[] _fields;
    object? _csharpObject;

    #region Constructor

    public StructNode(StructType type)
    {
        Type = type;
        
        // init fields
        _fields = type.GetFields().Select(p => p.Type?.Create() ?? throw new Exception($"The struct {type.Name}'s field {p.Name} has not valid value type")).ToArray();
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
    
    #endregion
    
    #region Implementation

    /// <summary>
    /// Gets the access value
    /// </summary>
    public override DataNode? GetAccessValue(ReadOnlySpan<char> source)
        => _fields.ElementAtOrDefault((Type as StructType)?.GetIndex(source) ?? -1);

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
    
    #endregion
}
