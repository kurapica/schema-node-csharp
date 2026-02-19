using System.Reflection;
using System.Runtime.Serialization;
using SchemaNode.Runtime;
using SchemaNode.Utility;
using System.Text.Json.Nodes;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Schema;

namespace SchemaNode.Node;

public class StructTypeNode : AnySchemaNode
{
    internal AnySchemaNode[] Fields;
    object? _csharpObject;

    public StructTypeNode(StructType type, object? value = null) : base(type, null)
    {
        // init fields
        Fields = new AnySchemaNode[type.Fields.Length];
        for(int i = 0; i < Fields.Length; i++)
        {
            var field = type.Fields[i];
            if (field.SchemeType == null)
            {
                throw new SerializationException($"The field {field.Name} type is not defined.");
            }

            Fields[i] = field.SchemeType!.CreateNode() ?? throw new NotSupportedException();
        }
        Value = value;
    }

    public object? this[string name]
    {
        get => GetField(name);
        set
        {
            var field = GetField(name);
            if (field != null)
            {
                _csharpObject = null;
                field.Value = value;
            }
        }
    }

    /// <summary>
    /// Get value with field name
    /// </summary>
    public AnySchemaNode? GetField(string fieldName)
    {
        var type = SchemaType as StructType;
        if (type == null) return null;
        var index = Array.FindIndex(type.Fields, f => f.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
        if (index < 0 || index >= Fields.Length) return null;
        return Fields[index];
    }

    public void SetField(string fieldName, object? value)
    {
        var field = GetField(fieldName);
        if (field != null)
        {
            _csharpObject = null;
            field.Value = value;
        }
    }

    /// <summary>
    /// Equals override
    /// </summary>
    public override bool Equals(AnySchemaNode other)
    {
        if (this == other) return true;
        if (other is not StructTypeNode otherStruct) return false;

        var fields = (SchemaType as StructType)!.Fields;
        foreach (var t in fields)
        {
            var field = otherStruct.GetField(t.Name);
            var thisField = GetField(t.Name);
            if (field == null || thisField == null || !field.Equals(thisField)) return false;
        }
        return true;
    }

    public override bool IsEmpty => Fields.All(f => f.IsEmpty);

    public override object? Value
    {
        get => this;
        set
        {
            _csharpObject = null;
            if (value == null)
            {
                foreach (AnySchemaNode @field in Fields)
                {
                    @field.Value = null;
                }
            }
            else if(value is StructTypeNode @struct)
            {
                var fields = (SchemaType as StructType)!.Fields;
                for (int i = 0; i < fields.Length; i++)
                {
                    Fields[i].Value = @struct.GetField(fields[i].Name);
                }
            }
            else if(value is JsonObject obj)
            {
                StructFieldConfig[] fields = (SchemaType as StructType)!.Fields;
                Dictionary<string, AnySchemaNode> fieldMap = [];
                JsonTypeNode? unpackNode = null;
                for (int i = 0; i < fields.Length; i++)
                {
                    fieldMap[fields[i].Name.ToLower()] = Fields[i];
                    if (fields[i].Unpack ?? false)
                        unpackNode = Fields[i] as JsonTypeNode;
                }

                JsonObject? packData = unpackNode != null ? new JsonObject() : null;
                foreach ((string key, JsonNode? val) in obj)
                {
                    if (fieldMap.TryGetValue(key.ToLower(), out AnySchemaNode? @field))
                    {
                        @field.Value = val;
                    }
                    else if (packData != null)
                    {
                        packData[key] = val?.DeepClone();
                    }
                }
                
                if (unpackNode != null && (unpackNode.Value is not JsonObject jobj || jobj.IsEmpty()))
                {
                    unpackNode.Value = packData;
                }
            }
            else if(value.GetType() == CsharpType)
            {
                IReadOnlyList<PropertyInfo>? props = (SchemaType as StructType)!.GetCSharpProperties();
                if (props != null)
                {
                    _csharpObject = value;
                    for (int i = 0; i < props.Count; i++)
                    {
                        Fields[i].Value = props[i]?.GetValue(value);
                    }
                }
                else
                {
                    JsonObject jsonObj = (JsonObject)value.ToJsonNode()!;
                    var fields = (SchemaType as StructType)!.Fields;
                    for (int i = 0; i < fields.Length; i++)
                    {
                        Fields[i].Value = jsonObj[fields[i].Name];
                    }
                }
            }
            else
            {
                throw new InvalidCastException($"Invalid value type {value.GetType()}");
            }
        }
    }

    /// <summary>
    /// Gets the value with paths
    /// </summary>
    internal AnySchemaNode? GetValueByPaths(IEnumerable<string> paths)
    {
        AnySchemaNode? node = this;
        foreach (string path in paths)
        {
            node = (node is StructTypeNode obj) ? obj.GetField(path) : null;
            if (node == null) return null;
        }
        return node;
    }

    /// <summary>
    /// Gets the value with paths
    /// </summary>
    public AnySchemaNode? GetValueByPaths(string paths) => GetValueByPaths(paths.Split('.', StringSplitOptions.RemoveEmptyEntries));

    public override object? ToTypeValue(Type type)
    {
        if (type.IsAssignableFrom(typeof(StructTypeNode))) return this;
        
        if (CsharpType.IsAssignableTo(type))
        {
            _csharpObject ??= ToJson()?.FromJson(CsharpType);
            return _csharpObject;
        }
        return ToJson()?.FromJson(type);
    }

    public override JsonNode? ToJson()
    {
        JsonObject result = [];

        var fields = (SchemaType as StructType)!.Fields;
        for (int i = 0; i < fields.Length; i++)
        {
            JsonNode? d = Fields[i].ToJson();
            if (d != null && !d.IsEmpty())
            {
                if (fields[i].Unpack ?? false)
                {
                    if (d is JsonObject obj)
                    {
                        foreach ((string key, JsonNode? val) in obj)
                        {
                            if (!result.ContainsKey(key) && val != null && !val.IsEmpty())
                                result[key] = val?.DeepClone();
                        }
                    }
                }
                else
                {
                    result.Add(fields[i].Name, d);
                }
            }
        }
        return result;
    }

    public override string ToString() => ToJson()?.ToString() ?? string.Empty;
}
