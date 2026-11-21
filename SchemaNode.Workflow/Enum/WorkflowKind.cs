using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.WorkflowConstant;

namespace SchemaNode.Enum.Workflow;

/// <summary>
/// BaseWorkflow type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_WORKFLOW}.kind")]
[Meta<Record>(typeof(Property.Workflow.WorkflowKind))]
public enum WorkflowKind;