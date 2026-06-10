namespace SchemaNode.Schema.Provider;

/// <summary>
/// The enum schema provider
/// </summary>
public interface IEnumSchemaProvider
{
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
}