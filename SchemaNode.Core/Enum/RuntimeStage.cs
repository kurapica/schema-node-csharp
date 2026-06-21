namespace SchemaNode.Enum;

/// <summary>
/// The runtime stage
/// </summary>
public enum RuntimeStage
{
    SystemSchemaLoading,
    SystemSchemaLoaded,
    SchemaLoading,
    SchemaLoaded,
    Activating,
    Activated,
    Deactivating,
    Deactivated
}