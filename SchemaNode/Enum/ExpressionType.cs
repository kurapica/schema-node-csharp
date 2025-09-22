using SchemaNode.Attribute;

namespace SchemaNode.Enum;

/// <summary>
/// The expression call type
/// </summary>
[SchemaEnum(EnumValueType.String)]
public enum ExpressionType
{
    /// <summary>
    /// Call directly
    /// </summary>
    Call,

    /// <summary>
    /// Map the array elements by the function
    /// </summary>
    Map,

    /// <summary>
    /// Reduce the array elements by the function
    /// </summary>
    Reduce,

    /// <summary>
    /// Gets the first element matched by the function
    /// </summary>
    First,

    /// <summary>
    /// Gets the last element matched by the function
    /// </summary>
    Last,

    /// <summary>
    /// Filter the array elements by the function
    /// </summary>
    Filter,
}