using System.Text.Json.Nodes;

namespace SchemaNode.Utility;

public static class JsonHelper
{
    /// <summary>
    /// Whether the json node is empty
    /// </summary>
    public static bool IsEmpty(this JsonNode? node)
    {
        if (node == null) return true;
        return node switch
        {
            JsonArray a => a.Count == 0,
            JsonObject o => o.Count == 0,
            JsonValue v => v.ToJsonString() == "null",
            _ => true
        };
    }
}