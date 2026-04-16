namespace SchemaNode.Runtime;

/// <summary>
/// The schema run-time with all run-time schema information, such as the schema types, properties, and so on.
/// It will be built by the stage handlers in the build stage and used in the runtime stage.
/// Normally it'd b as singleton instance for one service
/// </summary>
public class SchemaRuntime: ISchemaRunTime
{
    #region Utility

    private readonly NamespaceType _root = new();
    
    #endregion
}