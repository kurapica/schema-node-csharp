using SchemaNode.Attribute;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Enum;

/// <summary>
/// The app data field filter mode
/// </summary>
[Schema($"{NS_SYSTEM_SCHEMA_DEF_APP_FIELD}.filtermode")]
public enum FieldFilterMode
{
    /// <summary>
    /// Exactly contains
    /// </summary>
    Exactly = 1,
    
    /// <summary>
    /// Prefix contains
    /// </summary>
    Prefix = 2,
    
    /// <summary>
    /// Suffix contains
    /// </summary>
    Suffix = 3,
    
    /// <summary>
    /// Like / contains contains
    /// </summary>
    Contains = 4,
    
    /// <summary>
    /// The filter function
    /// </summary>
    Filter = 9,
}