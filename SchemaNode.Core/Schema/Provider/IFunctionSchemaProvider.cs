using System.Text.Json.Nodes;

namespace SchemaNode.Schema.Provider;

/// <summary>
/// The remote function call schema provider
/// </summary>
public interface IFunctionSchemaProvider
{
    /// <summary>
    /// Call the function with arguments and given generic type
    /// </summary>
    /// <param name="schemaName">The function schema name</param>
    /// <param name="args">The arguments</param>
    /// <param name="retType">The return type</param>
    /// <returns>The result</returns>
    Task<JsonNode?> CallFunctionAsync(string schemaName, JsonArray args, string? retType = null);
}