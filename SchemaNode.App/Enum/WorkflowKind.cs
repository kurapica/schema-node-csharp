using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.AppConstant;

namespace SchemaNode.Enum;

/// <summary>
/// BaseWorkflow type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_WORKFLOW}.kind")]
[Meta<Record>(typeof(Property.Record.WorkflowKind))]
public enum WorkflowKind;