using SchemaNode.Context;

namespace SchemaNode.Runtime;

/// <summary>
/// Represents a generic schema runtime that holds runtime type information. registered as a singleton for one service.
/// The runtime must be the SchemaRuntime or its child classes.
/// </summary>
public interface ISchemaRuntime
{
    /// <summary>
    /// Register the schema kind
    /// </summary>
    void RegisterSchemaKind(string kind, Type schemaType);
}
