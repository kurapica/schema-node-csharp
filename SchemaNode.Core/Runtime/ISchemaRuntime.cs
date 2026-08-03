using SchemaNode.Enum;
using SchemaNode.Property;

namespace SchemaNode.Runtime;

/// <summary>
/// Represents a generic schema runtime that holds runtime type information. registered as a singleton for one service.
/// The runtime must be the SchemaRuntime or its child classes.
/// </summary>
public interface ISchemaRuntime
{
    /// <summary>
    /// The current stage of the schema loading and runtime activation pipeline, it will be updated by the system and can be used to determine the current stage in the pipeline.
    /// </summary>
    RuntimeStage Stage { get; set; }

    /// <summary>
    /// Register the schema kind
    /// </summary>
    void RegisterSchemaKind(string kind, Type schemaType, Type[]? propertyTypes = null, IProperty[]? properties = null);
    
    /// <summary>
    /// Gets the schema kinds
    /// </summary>
    /// <returns></returns>
    IEnumerable<(string kind, Type schemaType)> GetSchemaKinds();
    
    /// <summary>
    /// Gets the schema properties
    /// </summary>
    /// <param name="kind"></param>
    /// <returns></returns>
    IEnumerable<Type> GetSchemaKindPropertyTypes(string kind);
    
    /// <summary>
    /// Gets schema kind property
    /// </summary>
    T? GetSchemaKindProperty<T>(string kind) where T: class, IProperty;
    
    /// <summary>
    /// Gets schema kind properties
    /// </summary>
    IEnumerable<T> GetSchemaKindProperties<T>(string kind) where T: IProperty;
}