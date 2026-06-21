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
    
    /// <summary>
    /// Applied when loading or use input
    /// </summary>
    LoadInput = Load | Input,

    /// <summary>
    /// Applied when load and persist
    /// </summary>
    LoadPersist = Load | Persist,
    
    /// <summary>
    /// Applied when input and persist
    /// </summary>
    InputPersist = Input | Persist,
    
    /// <summary>
    /// Applied in all stages: load, input and persist
    /// </summary>
    All = Load | Input | Persist,
}
