using System.Text.Json.Nodes;
using SchemaNode.Enum;
using SchemaNode.Schema;

namespace SchemaNode.Service;

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
    Task<NodeSchema[]> LoadSchemaAsync(string[] names);

    /// <summary>
    /// Load the enum value sub list
    /// </summary>
    /// <param name="schemaName">The enum schema name</param>
    /// <param name="value">The root enum value, optional</param>
    /// <param name="fullList">Whether load the full list</param>
    /// <returns></returns>
    Task<EnumValueSchema[]> LoadEnumSubListAsync(string schemaName, string? value, bool? fullList = null);
    
    /// <summary>
    /// Load the enum value access list from the server
    /// </summary>
    /// <param name="schemaName">The enum schema name</param>
    /// <param name="value">The enum value for access</param>
    /// <param name="noSubList">no sub list should be loaded</param>
    /// <param name="withSubList">with the value's sub list if existed</param>
    /// <returns></returns>
    Task<EnumValueAccess[]> LoadEnumAccessListAsync(string schemaName, string value, bool? noSubList = null, bool? withSubList = null);

    /// <summary>
    /// Call the function with arguments and given generic type
    /// </summary>
    /// <param name="schemaName">The function schema name</param>
    /// <param name="args">The arguments</param>
    /// <param name="retType">The return type</param>
    /// <param name="target">The related target</param>
    /// <returns>The result</returns>
    Task<JsonNode?> CallFunctionAsync(string schemaName, JsonArray args, string? retType = null, string? target = null);
}
