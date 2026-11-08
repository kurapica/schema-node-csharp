using SchemaNode.Struct;

namespace SchemaNode.Schema.Provider;

/// <summary>
/// The schema storage provider
/// </summary>
public interface IAppEntryStorageProvider: IAppEntryProvider
{
    /// <summary>
    /// Save the schema to the storage
    /// </summary>
    /// <param name="schema">The schema</param>
    /// <returns>true if saved</returns>
    Task<bool> SaveSchemaAsync(NodeSchema schema, CancellationToken token = default);

    /// <summary>
    /// Delete the schema from the storage
    /// </summary>
    /// <param name="schema">The schema</param>
    /// <param name="token">The cancellation token</param>
    /// <returns>true if deleted</returns>
    Task<bool> DeleteSchemaAsync(string schema, CancellationToken token = default);

    /// <summary>
    /// Save the enum entries of the parent value
    /// </summary>
    /// <param name="name">The schema name</param>
    /// <param name="value">The enum value</param>
    /// <param name="values">The enum sub list</param>
    /// <param name="append">Whether append the sub list not replace</param>
    /// <param name="token">The cancellation token</param>
    /// <returns>true if saved</returns>
    Task<bool> SaveEnumEntriesAsync(string name, string value, Entry<string>[] values, bool? append, CancellationToken token = default);

    /// <summary>
    /// Save the app schema
    /// </summary>
    /// <param name="app"></param>
    /// <param name="token">The cancellation token</param>
    /// <returns></returns>
    Task<bool> SaveAppSchemaAsync(AppSchema app, CancellationToken token = default);

    /// <summary>
    /// Delete an app schema
    /// </summary>
    /// <param name="app"></param>
    /// <param name="token">The cancellation token</param>
    /// <returns></returns>
    Task<bool> DeleteAppSchemaAsync(string app, CancellationToken token = default);

    /// <summary>
    /// Save app field schema
    /// </summary>
    Task<bool> SaveAppFieldSchemaAsync(string app, AppFieldSchema field, CancellationToken token = default);

    /// <summary>
    /// Delete app field schema
    /// </summary>
    Task<bool> DeleteAppFieldSchemaAsync(string app, string field, CancellationToken token = default);

    /// <summary>
    /// Swap the field order
    /// </summary>
    Task<bool> SwapAppFieldSchemaAsync(string app, string field1, string field2, CancellationToken token = default);

    /// <summary>
    /// Save app workflow schema
    /// </summary>
    Task<bool> SaveAppWorkflowSchemaAsync(string app, AppWorkflowSchema workflow, CancellationToken token = default);

    /// <summary>
    /// Delete app workflow schema
    /// </summary>
    Task<bool> DeleteAppWorkflowSchemaAsync(string app, string workflow, CancellationToken token = default);
}