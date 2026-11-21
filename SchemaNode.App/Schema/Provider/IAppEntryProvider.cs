namespace SchemaNode.Schema.Provider;

/// <summary>
/// Provider interface for loading app schemas from external sources
/// </summary>
public interface IAppEntryProvider : INodeSchemaProvider, IEnumEntryProvider
{
    /// <summary>
    /// Load the app schema information
    /// </summary>
    /// <param name="name">The application name</param>
    /// <param name="token">The cancellation token</param>
    /// <returns>The app schema</returns>
    Task<AppSchema?> GetAppSchemaAsync(string name, CancellationToken token = default);
}