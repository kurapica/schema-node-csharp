using System.Text.Json;
using System.Text.Json.Nodes;
using SchemaNode.Context;
using SchemaNode.Utility;

namespace SchemaNode.Schema;

/// <summary>
/// Public JSON bridge for components outside the <c>SchemaNode.App</c> assembly (for example the
/// AI chat module). It reuses the same serializer configuration as the HTTP API so node schema
/// extension properties (display, struct, enum, func, ...) are preserved.
/// </summary>
public static class SchemaNodeJsonExtension
{
    /// <summary>
    /// Gets the serializer options used by the schema HTTP API, including the property owner
    /// converter that flattens node schema extension properties.
    /// </summary>
    public static JsonSerializerOptions GetSerializerOptions() => JsonOptions.GetJsonOptions(false);

    /// <summary>
    /// Parses a node schema JSON object with the SchemaNode serializer configuration.
    /// </summary>
    /// <returns>The parsed schema, or <see langword="null"/> when the JSON is not a node schema.</returns>
    public static NodeSchema? ParseNodeSchema(this SchemaContext context, JsonNode? node)
    {
        if (node is not JsonObject) return null;
        try
        {
            return context.FromJsonRequest<NodeSchema>(node.ToJsonString());
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Converts a node schema to its normalized JSON representation. Runtime-only fields are
    /// removed and extension properties are flattened.
    /// </summary>
    public static JsonNode? ToSchemaJson(this SchemaContext context, NodeSchema? schema)
        => schema?.ToJsonNode();
}
