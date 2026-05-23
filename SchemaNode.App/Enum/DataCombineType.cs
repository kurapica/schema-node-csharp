using SchemaNode.Attribute;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Enum;

/// <summary>
/// The data combine type
/// </summary>
[Schema($"{NS_SYSTEM_SCHEMA_DEF_ARRAY}.{nameof(DataCombineType)}")]
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
}