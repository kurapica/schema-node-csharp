namespace SchemaNode.Schema.Provider;

/// <summary>
/// The schema storage provider
/// </summary>
public interface IAppSchemaStorageProvider: IAppSchemaProvider
{
    /// <summary>
    /// Save the schema to the storage
    /// </summary>
    /// <param name="schema">The schema</param>
    /// <returns>true if saved</returns>
    Task<bool> SaveSchemaAsync(NodeSchema schema);
    
    /// <summary>
    /// Delete the schema from the storage
    /// </summary>
    /// <param name="schema">The schema</param>
    /// <returns>true if deleted</returns>
    Task<bool> DeleteSchemaAsync(string schema);
    
    /// <summary>
    /// Save the sub list for an enum value
    /// </summary>
    /// <param name="name">The schema name</param>
    /// <param name="value">The enum value</param>
    /// <param name="values">The enum sub list</param>
    /// <param name="append">Whether append the sub list not replace</param>
    /// <returns>The new enum value info</returns>
    Task<EnumValueSchema[]> SaveEnumSubListAsync(string name, string? value, EnumValueSchema[] values, bool? append);
    
    /// <summary>
    /// Save the app schema
    /// </summary>
    /// <param name="app"></param>
    /// <returns></returns>
    Task<bool> SaveAppSchemaAsync(AppSchema app);

    /// <summary>
    /// Delete an app schema
    /// </summary>
    /// <param name="app"></param>
    /// <returns></returns>
    Task<bool> DeleteAppSchemaAsync(string app);

    /// <summary>
    /// Save app field schema
    /// </summary>
    Task<bool> SaveAppFieldSchemaAsync(string app, AppFieldSchema field);
    
    /// <summary>
    /// Delete app field schema
    /// </summary>
    Task<bool> DeleteAppFieldSchemaAsync(string app, string field);

    /// <summary>
    /// Swap the field order
    /// </summary>
    Task<bool> SwapAppFieldSchemaAsync(string app, string field1, string field2);
    
    /// <summary>
    /// Save app workflow schema
    /// </summary>
    Task<bool> SaveAppWorkflowSchemaAsync(string app, AppWorkflowSchema workflow);

    /// <summary>
    /// Delete app workflow schema
    /// </summary>
    Task<bool> DeleteAppWorkflowSchemaAsync(string app, string workflow);
}