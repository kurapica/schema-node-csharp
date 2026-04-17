using SchemaNode.Runtime;
using SchemaNode.Schema;

namespace SchemaNode.Service;

/// <summary>
/// Provider interface for loading schemas from external sources (database, file, etc.)
/// System schemas are handled internally by the runtime; this interface is for custom/user-defined schemas.
/// </summary>
public interface ISchemaProvider
{
    /// <summary>
    /// Load schemas by names. Returns null if not found.
    /// </summary>
    Task<NodeSchema[]?> LoadSchemaAsync(string[] names);

    /// <summary>
    /// The default load state for schemas loaded by this provider
    /// </summary>
    SchemaLoadState? DefaultLoadState { get; }
}
