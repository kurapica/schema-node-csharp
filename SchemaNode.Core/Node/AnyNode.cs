using System.Text.Json;
using System.Text.Json.Nodes;
using SchemaNode.Runtime;
using SchemaNode.Utility;

namespace SchemaNode.Node;

public class AnyNode: DataNode
{
    private const string IsoFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
    
    internal AnyNode(ObjectType type, object? value = null) : base(type, null)
    {
        Value = value switch
        {
            // init value
            System.Text.Json.Nodes.JsonNode jsonNode => ParseJsonNode(jsonNode),
            DataNode node => node.ToJsonNode(),
            _ => value?.ToJsonNode() ?? new JsonObject()
        };
    }

    public override bool Equals(DataNode other)
    {
        if (this == other) return true;
        if (other is not AnyNode otherJson) return false;

        return this.ToLiteral() == otherJson.ToLiteral();
    }
    
    JsonNode? ParseJsonNode(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
            {
                JsonObject jsonObject = new ();
                foreach (var (key, value) in obj)
                {
                    var childNode = ParseJsonNode(value);
                    if (childNode != null && !childNode.IsEmpty())
                        jsonObject[key] = childNode.DeepClone();
                }
                return jsonObject;
            }
            case JsonArray arr:
            {
                JsonArray res = [];
                foreach (JsonNode? n in arr)
                {
                    var childNode = ParseJsonNode(n);
                    if (childNode != null && !childNode.IsEmpty())
                        res.Add(childNode.DeepClone());
                }

                return res;
            }
            case JsonValue val when !val.IsEmpty() && val.GetValueKind() is JsonValueKind.String:
            {
                (object? v, _)  = val.ParseValueAndType();
                if (v != null)
                    return JsonValue.Create(v);
                break;
            }
        }

        return node;
    }
}