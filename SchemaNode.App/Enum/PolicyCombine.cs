using SchemaNode.Attribute;
using static SchemaNode.Utility.AppConstant;

namespace SchemaNode.Enum;

/// <summary>
/// The policy combine
/// </summary>
[Schema($"{NS_SYSTEM_SCHEMA_POLICY}.combine")]
public enum PolicyCombine
{
    /// <summary>
    /// auth1 && auth2
    /// </summary>
    AndAlso = 1,
    
    /// <summary>
    /// auth1 || auth2
    /// </summary>
    OrElse = 2,
}