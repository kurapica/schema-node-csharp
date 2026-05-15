using System.Reflection;
using System.Runtime.Serialization;
using SchemaNode.Utility;
using System.Text.Json.Nodes;
using SchemaNode.Schema;
using StructType = SchemaNode.Runtime.StructType;

namespace SchemaNode.Node;

public class StructNode : DataNode, IDictionary<string, DataNode>
{
    internal DataNode[] Fields;
    object? _csharpObject;

    public StructNode(StructType type, object? value = null) : base(type, null)
    {
        // init fields
        Fields = new DataNode[type.Fields.Length];
        for(int i = 0; i < Fields.Length; i++)
        {
            var field = type.Fields[i];
            if (field.SchemaType == null)
            {
                throw new SerializationException($"The field {field.Name} type is not defined.");
            }

            Fields[i] = field.SchemaType!.CreateNode() ?? throw new NotSupportedException();
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

    public DataNode? GetField(ReadOnlySpan<char> segment) => Fields.ElementAtOrDefault((NodeType as StructType)!.GetIndex(segment));

    /// <summary>
    /// Gets the field schema
    /// </summary>
    public StructFieldSchema? GetFieldSchema(DataNode? node)
    {
        if (node == null) return null;
        var type = NodeType as StructType;
        if (type == null) return null;
        var index = Array.FindIndex(Fields, f => f == node);
        if (index < 0 || index >= type.Fields.Length) return null;
        return type.Fields[index];
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
    public override bool Equals(DataNode other)
    {
        if (this == other) return true;
        if (other is not StructNode otherStruct) return false;

        var fields = (NodeType as StructType)!.Fields;
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
                foreach (DataNode @field in Fields)
                {
                    @field.Value = null;
                }
            }
            else if(value is StructNode @struct)
            {
                var fields = (NodeType as StructType)!.Fields;
                for (int i = 0; i < fields.Length; i++)
                {
                    Fields[i].Value = @struct.GetField(fields[i].Name);
                }
            }
            else if(value is JsonObject obj)
            {
                StructFieldSchema[] fields = (NodeType as StructType)!.Fields;
                Dictionary<string, DataNode> fieldMap = [];
                AnyNode? unpackNode = null;
                for (int i = 0; i < fields.Length; i++)
                {
                    fieldMap[fields[i].Name.ToLower()] = Fields[i];
                    if (fields[i].Unpack ?? false)
                        unpackNode = Fields[i] as AnyNode;
                }

                JsonObject? packData = unpackNode != null ? new JsonObject() : null;
                foreach ((string key, System.Text.Json.Nodes.JsonNode? val) in obj)
                {
                    if (fieldMap.TryGetValue(key.ToLower(), out DataNode? @field))
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
                IReadOnlyList<PropertyInfo>? props = (NodeType as StructType)!.GetCSharpProperties();
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
                    var fields = (NodeType as StructType)!.Fields;
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
    internal DataNode? GetValueByPaths(SpanReader spans)
    {
        DataNode? node = this;
        while (spans.NextNamespace(out ReadOnlySpan<char> part))
        {
            node = (node is StructNode obj) ? obj.GetField(part) : null;
            if (node == null) return null;
        }
        return node;
    }

    /// <summary>
    /// Gets the value with paths
    /// </summary>
    public DataNode? GetValueByPaths(string paths) => GetValueByPaths(new SpanReader(paths));

    public override object? ToTypeValue(Type type)
    {
        if (type.IsAssignableFrom(typeof(StructNode))) return this;
        
        if (CsharpType.IsAssignableTo(type))
        {
            _csharpObject ??= ToJson()?.FromJson(CsharpType);
            return _csharpObject;
        }
        return ToJson()?.FromJson(type);
    }

    public override System.Text.Json.Nodes.JsonNode? ToJson()
    {
        JsonObject result = [];

        var fields = (NodeType as StructType)!.Fields;
        for (int i = 0; i < fields.Length; i++)
        {
            System.Text.Json.Nodes.JsonNode? d = Fields[i].ToJson();
            if (d != null && !d.IsEmpty())
            {
                if (fields[i].Unpack ?? false)
                {
                    if (d is JsonObject obj)
                    {
                        foreach ((string key, System.Text.Json.Nodes.JsonNode? val) in obj)
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
