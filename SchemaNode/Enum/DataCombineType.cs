using SchemaNode.Attribute;

namespace SchemaNode.Enum;

/// <summary>
/// The data combine type
/// </summary>
[SchemaEnum(EnumValueType.String)]
public enum DataCombineType
{
    /// <summary>
    /// Assign
    /// </summary>
    Assign,
    
    /// <summary>
    /// Init
    /// </summary>
    Init,

    /// <summary>
    /// Sum
    /// </summary>
    Sum,

    /// <summary>
    /// Count
    /// </summary>
    Count,

    /// <summary>
    /// The min value
    /// </summary>
    Min,

    /// <summary>
    /// The max value
    /// </summary>
    Max,
}