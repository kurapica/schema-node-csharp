using SchemaNode.Attribute;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Enum;

/// <summary>
/// The application target policy type
/// </summary>
[Schema($"{NS_SYSTEM_SCHEMA_DEF_APP}.scope")]
public enum AppScopeType
{
    /// <summary>
    /// Use target, the default policy
    /// </summary>
    BusinessTarget = 0,
    
    /// <summary>
    /// No target, system app
    /// </summary>
    SystemLevel = 1,
    
    /// <summary>
    /// Use context item for data isolation like tenate id, org id
    /// </summary>
    IsolationContext = 2,
}