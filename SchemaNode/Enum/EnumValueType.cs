using SchemaNode.Attribute;

namespace SchemaNode.Enum;

/// <summary>
/// The value type of the enum.
/// </summary>
[SchemaEnum(EnumValueType.String)]
public enum EnumValueType
{
    /// <summary>
    /// The enum value is a string.
    /// </summary>
    String,

    /// <summary>
    /// The enum value is an integer.
    /// </summary>
    Int,

    /// <summary>
    /// The enum value is flags.
    /// </summary>
    Flags,
}