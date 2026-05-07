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
}
