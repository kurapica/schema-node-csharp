using System.Text.Json.Nodes;
using SchemaNode.Schema;

namespace SchemaNode.Provider;

/// <summary>
/// The schema provider, used to load schema from storages, network or other resources
/// </summary>
public interface ISchemaProvider
{
    /// <summary>
    /// Load the schema information
    /// </summary>
    /// <param name="schemaName">The schema name</param>
    /// <returns>The schema</returns>
    Task<NodeSchema?> LoadSchemaAsync(string schemaName);

    /// <summary>
    /// Load the application schema information
    /// </summary>
    /// <param name="app">The application name</param>
    /// <returns>The application schema</returns>
    Task<AppSchema?> LoadAppSchemaAsync(string app);

    /// <summary>
    /// Load the enum value sub list
    /// </summary>
    /// <param name="schemaName">The enum schema name</param>
    /// <param name="value">The root enum value, optional</param>
    /// <param name="fullList">Whether load the full list</param>
    /// <returns></returns>
    Task<EnumValueInfo[]> LoadEnumSubListAsync(string schemaName, string? value, bool? fullList = null);
    
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
    /// <param name="generic">The generic types</param>
    /// <returns>The result</returns>
    Task<JsonNode> CallFunctionAsync(string schemaName, JsonArray args, string[]? generic = null);
}