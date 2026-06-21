using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.AppConstant;

namespace SchemaNode.Enum;

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