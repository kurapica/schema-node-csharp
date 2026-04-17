using SchemaNode.Runtime;

namespace SchemaNode.Context;

/// <summary>
/// Represents the schema context to access the runtime
/// </summary>
public interface ISchemaContext: IDisposable
{
    /// <summary>
    /// The schema runtime
    /// </summary>
    ISchemaRuntime Runtime { get; } 
}