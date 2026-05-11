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
    void RegisterSchemaKind(string kind, Type schemaType, Type[]? properties = null);
    
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
    IEnumerable<Type> GetSchemaKindProperties(string kind);
    
    /// <summary>
    /// Gets the schema property with specific value type
    /// </summary>
    /// <param name="kind"></param>
    /// <param name="valueType"></param>
    /// <returns></returns>
    Type? GetSchemaKindProperty(string kind, Type valueType);
    
    /// <summary>
    /// Gets the schema property by property name
    /// </summary>
    /// <param name="kind"></param>
    /// <param name="propertyName"></param>
    /// <returns></returns>
    Type? GetSchemaKindPropertyByName(string kind, string propertyName);
}