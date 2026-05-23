using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Enum;

[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_ARRAY}.combinetype")]
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