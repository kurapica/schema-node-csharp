using SchemaNode.Attribute;
using static SchemaNode.Utility.AppConstant;

namespace SchemaNode.Enum;

/// <summary>
/// The field filter resolve type
/// </summary>
[Schema($"{NS_SYSTEM_SCHEMA_APP_FIELD}.filterresolve")]
public enum FieldFilterResolve
{
    /// <summary>
    /// Query cascade parent node if no contains
    /// </summary>
    CascadeParent = 1,
}