using SchemaNode.Schema;

namespace SchemaNode.Attribute;

/// <summary>
/// Declare system namespace
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Method)]
public class SchemaTypeAttribute: System.Attribute
{
    /// <summary>
    /// The namespace name
    /// </summary>
    public string? Name { get; }
    
    /// <summary>
    /// The display
    /// </summary>
    public string? Display { get; }

    /// <summary>
    /// The constructor
    /// </summary>
    /// <param name="name">The namespace</param>
    /// <param name="display">The display</param>
    public SchemaTypeAttribute(string? name = null, string? display = null)
    {
        Name = name;
        Display = display;
    }
}