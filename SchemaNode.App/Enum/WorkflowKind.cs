using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.AppConstant;

namespace SchemaNode.Enum;


/// <summary>
/// Workflow type
/// </summary>
[Schema($"{NS_SYSTEM_SCHEMA_WORKFLOW}.kind")]
[Meta<Record>(typeof(SchemaNode.Property.Record.WorkflowKind))]
public enum WorkflowKind
{
    /// <summary>
    /// Use arguments for controlling
    /// </summary>
    Workflow = 1,
    
    /// <summary>
    /// Use function with arguments for calling
    /// </summary>
    Function,
    
    /// <summary>
    /// Use event with arguments for waiting
    /// </summary>
    Event,
    
    /// <summary>
    /// Use interaction for user interaction
    /// </summary>
    Interaction,
}
