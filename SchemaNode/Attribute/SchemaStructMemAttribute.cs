
using SchemaNode.Schema;

namespace SchemaNode.Attribute;

/// <summary>
/// The struct member schema info
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class SchemaStructMemAttribute: System.Attribute
{
    public string? Type { get; }

    /// <summary>
    /// The display of the enum
    /// </summary>
    public string? Display { get; }
    
    /// <summary>
    /// The description of the enum
    /// </summary>
    public string? Desc { get; }
    
    /// <summary>
    /// The field is display only
    /// </summary>
    public bool DisplayOnly { get; }
    
    /// <summary>
    /// The constructor
    /// </summary>
    public SchemaStructMemAttribute(string? type = null, string? display = null, string? desc = null, bool displayOnly = false)
    {
        Type = type;
        Display = display;
        Desc = desc;
        DisplayOnly = displayOnly;
    }
}
/// <summary>
/// The struct member schema info
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class SchemaStructMemIgnoreAttribute: System.Attribute
{
}