using SchemaNode.Enum;

namespace SchemaNode.Attribute;

// Sets the target policy for shema
[AttributeUsage(AttributeTargets.Assembly)]
public class SchemaAppScopeAttribute(AppScopeType type, string? contextItem = null, string? mapKey = null): System.Attribute
{
    /// <summary>
    /// The app target policy type
    /// </summary>
    public AppScopeType Type { get; } = type;
    
    /// <summary>
    /// The context item as the data isolation
    /// </summary>
    public string? ContextItem { get; } = contextItem;
    
    /// <summary>
    /// The map key for the context item
    /// </summary>
    public string? MapKey { get; } = mapKey;
}