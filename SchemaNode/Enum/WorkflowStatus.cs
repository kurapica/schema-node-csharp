using SchemaNode.Attribute;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Enum;

/// <summary>
/// The workflow status enum
/// </summary>
[Schema($"{NS_SYSTEM_WORKFLOW}.status")]
public enum WorkflowStatus
{
    Waiting,
    Running,
    Done,
    Error,
    Terminated
}