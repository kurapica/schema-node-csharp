using SchemaNode.Attribute;
using SchemaNode.Function;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Record;
using SchemaNode.Property.Property;
using SchemaNode.Schema;
using static SchemaNode.Utility.AppConstant;
using static SchemaNode.Utility.Constant;
using SchemaType = SchemaNode.Property.Core.SchemaType;

namespace SchemaNode.AI.Workflow;

/// <summary>
/// The target schema type property of the AI workflow node. Editing it in Man updates the node's
/// declared payload type through the same default relation used by call/event workflow nodes.
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_APP_WORKFLOW_NODE)]
[Meta<OfNodeKind>(NODE_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP}.workflow.ai.target")]
[Relation<Default, Relation.Call>("aiTarget", $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(AppWorkflowNodeSchema.Payload)}")]
public sealed class AITargetProperty : Property<string>;
