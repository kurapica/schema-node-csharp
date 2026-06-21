namespace SchemaNode.Schema.Provider;

/// <summary>
/// Provider interface for loading app schemas from external sources
/// </summary>
public interface IAppSchemaProvider : INodeSchemaProvider, IEnumSchemaProvider
{
    /// <summary>
    /// Load the app schema information
    /// </summary>
    /// <param name="name">The application name</param>
    /// <returns>The app schema</returns>
    Task<AppSchema?> LoadAppSchemaAsync(string name);
}