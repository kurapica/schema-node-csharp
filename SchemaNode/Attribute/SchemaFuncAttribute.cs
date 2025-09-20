using SchemaNode.Schema;

namespace SchemaNode.Attribute;

/// <summary>
/// Declare a static method to be registered as system function
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class SchemaFuncAttribute: System.Attribute
{
    /// <summary>
    /// The description of the function
    /// </summary>
    public LocaleString? Display { get; }
    
    /// <summary>
    /// The data dict type
    /// </summary>
    public string? Type { get; }

    /// <summary>
    /// The constructor
    /// </summary>
    public SchemaFuncAttribute(LocaleString? display = null, string? type = null)
    {
        Display = display;
        Type = type;
    }
}