using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.AppConstant;

namespace SchemaNode.Enum;

[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP_FIELD}.combinetype")]
public enum DataCombineType
{
    /// <summary>
    /// Assign
    /// </summary>
    Newest,
    
    /// <summary>
    /// Init
    /// </summary>
    Oldest,

    /// <summary>
    /// Sum
    /// </summary>
    Sum,

    /// <summary>
    /// Count
    /// </summary>
    Count,
}