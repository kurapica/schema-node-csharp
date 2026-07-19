using SchemaNode.Struct;
using System.Text.Json.Nodes;

namespace SchemaNode.Schema.Provider;

/// <summary>
/// Provider interface for loading node schemas from external sources (database, file, etc.)
/// System schemas are handled internally by the runtime; this interface is for custom/user-defined schemas.
/// </summary>
public interface INodeSchemaProvider
{
    /// <summary>
    /// Load the schema information
    /// </summary>
    /// <param name="names">The schema names</param>
    /// <returns>The schema</returns>
    Task<NodeSchema[]> GetSchemaAsync(string[] names);

    /// <summary>
    /// Gets the enum entry access list by value
    /// </summary>
    /// <param name="schemaName">The enum type</param>
    /// <param name="value">The given value, if null get the children of the start value</param>
    /// <param name="start">The access start value</param>
    /// <returns></returns>
    Task<EntryAccess<string>[]> GetEnumEntryAccess(string schemaName, string? value, string? start = null);

    /// <summary>
    /// Call the function with arguments and given generic type
    /// </summary>
    /// <param name="schemaName">The function schema name</param>
    /// <param name="args">The arguments</param>
    /// <param name="retType">The return type</param>
    /// <returns>The result</returns>
    Task<JsonNode?> CallFunctionAsync(string schemaName, JsonArray args, string? retType = null);
}
