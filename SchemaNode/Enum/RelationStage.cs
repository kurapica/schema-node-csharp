using SchemaNode.Attribute;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Enum;

[Schema($"{NS_SYSTEM_SCHEMA_DEF_STRUCT}.relation.stage")]
[Flags]
public enum RelationStage
{
    /// <summary>
    /// Applied when loading data
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
