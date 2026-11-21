using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.WorkflowConstant;

namespace SchemaNode.Enum.Workflow;

/// <summary>
/// The workflow status enum
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_WORKFLOW}.status")]
public enum WorkflowStatus
{
    Waiting,
    Running,
    Done,
    Error,
    Terminated
}