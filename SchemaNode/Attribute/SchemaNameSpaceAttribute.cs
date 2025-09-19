namespace SchemaNode.Attribute;

/// <summary>
/// Declare system namespace
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly)]
public class SchemaNameSpaceAttribute: System.Attribute
{
    /// <summary>
    /// The namespace name
    /// </summary>
    public string Name { get; }
    
    /// <summary>
    /// The display
    /// </summary>
    public string? Display { get; }

    /// <summary>
    /// The constructor
    /// </summary>
    /// <param name="name">The namespace</param>
    /// <param name="display">The display</param>
    public SchemaNameSpaceAttribute(string name, string? display = null)
    {
        Name = name;
        Display = display;
    }
}