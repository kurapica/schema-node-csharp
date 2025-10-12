using SchemaNode.Runtime;
using SchemaNode.Utility;
using System.Text.Json.Nodes;

namespace SchemaNode.Node;

public class StructNode : AnySchemaNode
{
    internal AnySchemaNode[] Fields = [];
    object? csharpObject = null;

    public StructNode(StructType type, object? value = null) : base(type, null)
    {
        // init fields
        Fields = new AnySchemaNode[type.Fields.Length];
        for(int i = 0; i < Fields.Length; i++)
        {
            var field = type.Fields[i];
            Fields[i] = field.TypeNode!.CreateNode() ?? throw new NotSupportedException();
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
                csharpObject = null;
                field.Value = value;
            }
        }
    }

    /// <summary>
    /// Get value with field name
    /// </summary>
    /// <param name="fieldName"></param>
    /// <returns></returns>
    public AnySchemaNode? GetField(string fieldName)
    {
        var type = Type as StructType;
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
            csharpObject = null;
            field.Value = value;
        }
    }

    public override bool IsEmpty => Fields.All(f => f.IsEmpty);

    public override object? Value
    {
        get => this;
        set
        {
            csharpObject = null;
            if (value == null)
            {
                foreach (var field in Fields)
                {
                    field.Value = null;
                }
            }
            else if(value is StructNode @struct)
            {
                var fields = (Type as StructType)!.Fields;
                for (int i = 0; i < fields.Length; i++)
                {
                    Fields[i].Value = @struct.GetField(fields[i].Name);
                }
            }
            else if(value is JsonObject obj)
            {
                var fields = (Type as StructType)!.Fields;
                for (int i = 0; i < fields.Length; i++)
                {
                    Fields[i].Value = obj[fields[i].Name];
                }
            }
            else if(value.GetType() == CsharpType)
            {
                csharpObject = value;
                var fields = (Type as StructType)!.Fields;
                for (int i = 0; i < fields.Length; i++)
                {
                    Fields[i].Value = CsharpType.GetProperty(fields[i].Name)?.GetValue(value);
                }
            }
            else
            {
                throw new InvalidCastException();
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
            node = (node is StructNode obj) ? obj.GetField(path) : null;
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
        if (CsharpType.IsAssignableTo(type))
        {
            csharpObject ??= ToJson()?.FromJson(CsharpType);
            return csharpObject;
        }
        return ToJson()?.FromJson(type);
    }

    public override JsonNode? ToJson()
    {
        JsonObject result = [];

        var fields = (Type as StructType)!.Fields;
        for (int i = 0; i < fields.Length; i++)
        {
            JsonNode? d = Fields[i].ToJson();
            if (d != null) result.Add(fields[i].Name, d);
        }
        return result;
    }
}
