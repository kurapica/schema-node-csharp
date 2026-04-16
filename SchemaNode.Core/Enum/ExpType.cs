using SchemaNode.Attribute;
using SchemaNode.Property.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Enum;

/// <summary>
/// The expression call type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_FUNC}.exptype")]
public enum ExpType
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
    
    /// <summary>
    /// Count the elements matched by the function
    /// </summary>
    Count,
    
    /// <summary>
    /// All elements must contains the function
    /// </summary>
    All,
    
    /// <summary>
    /// Any element matches the function
    /// </summary>
    Any,
}