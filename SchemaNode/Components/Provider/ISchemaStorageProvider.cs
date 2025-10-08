using SchemaNode.Node;
using SchemaNode.Schema;

namespace SchemaNode.Components.Provider;

/// <summary>
/// The schema storage provider
/// </summary>
public interface ISchemaStorageProvider: ISchemaProvider
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
    /// <param name="schema">The schema name</param>
    /// <param name="value">The enum value</param>
    /// <param name="values">The enum sub list</param>
    /// <param name="append">Whether append the sub list not replace</param>
    /// <returns>true if saved</returns>
    Task<bool> SaveEnumSubListAsync(EnumNode schema, string? value, EnumValueInfo[] values, bool? append);
    
    /// <summary>
    /// Delete the sub list for an enum value
    /// </summary>
    /// <param name="schema">The schema name</param>
    /// <param name="value">The enum value</param>
    /// <returns>true if deleted</returns>
    Task<bool> DeleteEnumSubListAsync(EnumNode schema, string value);

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
    /// <param name="app"></param>
    /// <param name="field1"></param>
    /// <param name="field2"></param>
    /// <returns></returns>
    Task<bool> SwapAppFieldSchemaAsync(string app, string field1, string field2);
}