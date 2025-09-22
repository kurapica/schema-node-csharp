using System.Runtime.Serialization;
using SchemaNode.Attribute;

namespace SchemaNode.Enum;

/// <summary>
/// Schema types.
/// </summary>
[SchemaEnum(EnumValueType.String)]
public enum SchemaType
{
    /// <summary>
    /// The namespace node
    /// </summary>
    Namespace,

    /// <summary>
    /// The scalar node
    /// </summary>
    Scalar,

    /// <summary>
    /// The num node
    /// </summary>
    Enum,

    /// <summary>
    /// The struct node
    /// </summary>
    Struct,

    /// <summary>
    /// The array node
    /// </summary>
    Array,

    /// <summary>
    /// The function node
    /// </summary>
    [EnumMember(Value = "func")]
    Function,
}