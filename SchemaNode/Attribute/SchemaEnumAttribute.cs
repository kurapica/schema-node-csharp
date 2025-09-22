using SchemaNode.Enum;
using SchemaNode.Schema;

namespace SchemaNode.Attribute;

/// <summary>
/// Register a enum as system enum
/// </summary>
[AttributeUsage(AttributeTargets.Enum)]
public class SchemaEnumAttribute: System.Attribute
{
    /// <summary>
    /// The description of the enum
    /// </summary>
    public string? Display { get; }
    
    /// <summary>
    /// The enum value type
    /// </summary>
    public EnumValueType? ValueType { get; }
    
    /// <summary>
    /// The data dict type
    /// </summary>
    public string? Type { get; }
   

    /// <summary>
    /// The constructor
    /// </summary>
    public SchemaEnumAttribute(string? display = null, string? type = null)
    {
        Display = display;
        Type = type;
    }
    
    /// <summary>
    /// The constructor
    /// </summary>
    public SchemaEnumAttribute(EnumValueType valueType, string? display = null, string? type = null)
    {
        Display = display;
        ValueType = valueType;
        Type = type;
    }
}