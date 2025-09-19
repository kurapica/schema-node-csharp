using SchemaNode.Enum;

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
    /// Whether generate the array type
    /// </summary>
    public bool? Array { get; }

    /// <summary>
    /// The constructor
    /// </summary>
    public SchemaEnumAttribute(string? display = null, EnumValueType? valueType = null, string? type = null, bool? array = null)
    {
        Display = display;
        ValueType = valueType;
        Type = type;
        Array = array;
    }
}