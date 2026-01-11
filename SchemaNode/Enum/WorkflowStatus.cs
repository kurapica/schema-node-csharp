using SchemaNode.Attribute;

namespace SchemaNode.Enum;

/// <summary>
/// The workflow status enum
/// </summary>
public enum WorkflowStatus
{
    Waiting,
    Running,
    Done,
    Error,
    Terminated
}