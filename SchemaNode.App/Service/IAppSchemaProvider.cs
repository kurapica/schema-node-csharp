using SchemaNode.Schema;

namespace SchemaNode.Service;

/// <summary>
/// Provider interface for loading app schemas from external sources
/// </summary>
public interface IAppSchemaProvider : INodeSchemaProvider
{
    /// <summary>
    /// Load the app schema information
    /// </summary>
    /// <param name="names">The application names</param>
    /// <returns>The app schemas</returns>
    Task<AppSchema[]> LoadAppSchemaAsync(string[] names);
}