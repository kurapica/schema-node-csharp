
using SchemaNode.Schema;

namespace SchemaNode.Attribute;

/// <summary>
/// The struct member schema info
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class SchemaStructMemAttribute: System.Attribute
{
    /// <summary>
    /// The display of the enum
    /// </summary>
    public LocaleString? Display { get; }
    
    /// <summary>
    /// The description of the enum
    /// </summary>
    public LocaleString? Desc { get; }

    /// <summary>
    /// The constructor
    /// </summary>
    public SchemaStructMemAttribute(LocaleString? display = null, LocaleString? desc = null)
    {
        Display = display;
        Desc = desc;
    }
}
/// <summary>
/// The struct member schema info
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class SchemaStructMemIgnoreAttribute: System.Attribute
{
}