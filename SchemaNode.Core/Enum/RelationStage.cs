using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Enum;

/// <summary>
/// The relation stage
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_RELATION}.stage")]
[Flags]
public enum RelationStage
{
    /// <summary>
    /// Applied when loading data or initializing data
    /// </summary>
    Load = 1,

    /// <summary>
    /// Applied when user input data
    /// </summary>
    Input = 2,

    /// <summary>
    /// Applied when saving data
    /// </summary>
    Persist = 4,
}
