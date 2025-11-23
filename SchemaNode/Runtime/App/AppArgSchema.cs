using System.Text.Json.Nodes;

namespace SchemaNode.Runtime;


/// <summary>
/// The app argument
/// </summary>
public class AppArgSchema
{
    /// <summary>
    /// The application field
    /// </summary>
    public string? AppField { get; init; }

    /// <summary>
    /// The data field
    /// </summary>
    public string? DataField { get; init; }

    /// <summary>
    /// The json value
    /// </summary>
    public JsonNode? Value { get; init; }
}