using System.Text.Json.Nodes;

namespace SchemaNode.Components;

/// <summary>
/// The function workflow class
/// </summary>
public abstract class FunctionWorkflow: Workflow
{
    /// <summary>
    /// Call the given function with arguments
    /// </summary>
    public abstract Task<JsonNode?> ExecuteAsync(string func, JsonArray args);
}