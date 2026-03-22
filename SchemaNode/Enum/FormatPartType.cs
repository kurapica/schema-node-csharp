using SchemaNode.Attribute;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Enum;

/// <summary>
/// The format part type used by the recognizer Parts configuration
/// </summary>
[Schema($"{NS_SYSTEM_SCHEMA_DEF_RECOGNIZER}.parttype")]
public enum FormatPartType
{
    /// <summary>
    /// A literal text part
    /// </summary>
    Literal,

    /// <summary>
    /// A field reference part bound to a struct field
    /// </summary>
    Field,

    /// <summary>
    /// A self reference part bound to the data itself, used for scalar, enum
    /// </summary>
    Self,

    /// <summary>
    /// An element reference part used for array elements
    /// </summary>
    Elements,
}