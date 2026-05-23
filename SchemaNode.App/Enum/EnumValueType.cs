using SchemaNode.Attribute;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Enum;

/// <summary>
/// The value type of the enum.
/// </summary>
[Schema($"{NS_SYSTEM_SCHEMA_DEF_ENUM}.valuetype")]
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