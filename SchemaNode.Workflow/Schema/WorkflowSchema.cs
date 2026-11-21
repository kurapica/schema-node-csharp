using SchemaNode.Attribute;
using SchemaNode.Function;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Property;
using SchemaNode.Property.Struct;
using SchemaNode.Relation;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.WorkflowConstant;
using NodeKind = SchemaNode.Property.Record.NodeKind;
using NodeType = SchemaNode.Property.Core.NodeType;
using SchemaKind = SchemaNode.Property.Record.SchemaKind;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Schema;

[Meta<SchemaKind>(SCHEMA_KIND_NODE_WORKFLOW, SCHEMA_KIND_ORDER_WORKFLOW)]
[Meta<NodeKind>(NODE_KIND_WORKFLOW, SCHEMA_KIND_ORDER_FUNC)]
[Meta<NodeType>(typeof(Runtime.WorkflowType))]
public sealed class WorkflowTypeKind;

/// <summary>
/// The workflow schema
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_WORKFLOW}.schema")]
[Meta<Attach>(SCHEMA_KIND_NODE_WORKFLOW)]
[Meta<DataNodeType>(typeof(WorkflowNode))]
[Meta<EntrySourceProvider>($"{NS_SYSTEM_SCHEMA_REFLECT_WORKFLOW}.{nameof(SystemReflectWorkflow.getaccessentries)}", $"@{nameof(Args)}", $"@{nameof(Steps)}.{ARRAY_PREVIOUS}", $"@{nameof(Steps)}.{ARRAY_ELEMENT}.{nameof(WorkflowStepSchema.Previous)}", NODE_SELF)]
[Meta<AccessValueTypeProvider>($"{NS_SYSTEM_SCHEMA_REFLECT_WORKFLOW}.{nameof(SystemReflectWorkflow.getaccessvaluetype)}", $"@{nameof(Args)}", $"@{nameof(Steps)}.{ARRAY_PREVIOUS}", NODE_SELF)]
[Meta<KindProvider>(SCHEMA_KIND_NODE_FUNC_ARG)]
[Relation<EntrySource, Assign>($"{nameof(Steps)}.{ARRAY_ELEMENT}.{nameof(WorkflowStepSchema.Previous)}.{ARRAY_ELEMENT}", $"{NS_SYSTEM_SCHEMA_REFLECT_WORKFLOW}.{nameof(SystemReflectWorkflow.getaccessentries)}", $"@{nameof(Steps)}.{ARRAY_PREVIOUS}")]
public sealed class WorkflowSchema: PropertyOwner
{
    /// <summary>
    /// The payload type of the workflow if provided, only non-fork workflow with payload can be used as sub-workflow
    /// </summary>
    [Meta<SchemaType>(typeof(ValueType))]
    public string? Payload { get; set; }

    /// <summary>
    /// The workflow arguments, forkable workflow can't have payload and arguments
    /// </summary>
    [Meta<DataNodeType>(typeof(FuncArgsNode))]
    [Relation<Visible, Call>(NODE_SELF, $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.notempty)}", $"@{nameof(Payload)}")]
    public FuncArg[]? Args { get; set; } = [];

    /// <summary>
    /// Whether persistence the workflow
    /// </summary>
    public bool? Persistence { get; set; }

    /// <summary>
    /// Enable full mode in the workflow context
    /// </summary>
    public bool? FullMode { get; set; }

    /// <summary>
    /// The workflow steps
    /// </summary>
    public WorkflowStepSchema[] Steps { get; set; } = [];
}

/// <summary>
/// Declare the workflow definition for a workflow node type
/// </summary>
[Meta<Alias>(NODE_KIND_WORKFLOW)]
[Meta<ForSchema>(SCHEMA_KIND_NODE)]
[Meta<OfNodeKind>(NODE_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_WORKFLOW}.{NODE_KIND_WORKFLOW}")]
[Meta<NoInherit>(true)]
[Relation<Visible, Call>(NODE_KIND_WORKFLOW, NS_SYSTEM_LOGIC_EQ, $"@{nameof(NodeSchema.Kind)}", NODE_KIND_WORKFLOW)]
public sealed class WorkflowProperty: Property<WorkflowSchema>
{
    public override bool Combine(IProperty other, ISchemaRuntime? runtime = null)
    {
        if (other is not WorkflowProperty { Value: { } otherSchema }) return false;
        if (Value is not { } schema)
        {
            SetValue(otherSchema);
            return true;
        }

        // Combine argument display
        if (schema.Args != null)
        {
            for (int i = 0; i < schema.Args.Length; i++)
            {
                var arg = schema.Args[i];
                var otherArg = otherSchema.Args?.ElementAtOrDefault(i);
                if (otherArg is null || otherArg.Type != arg.Type) continue;
                arg.CombineProperties(otherArg, runtime, SCHEMA_KIND_NODE_FUNC_ARG);
            }
        }

        schema.CombineProperties(otherSchema, runtime, SCHEMA_KIND_NODE_WORKFLOW);
        SetValue(schema);
        return true;
    }
}

/// <summary>
/// Represents the workflow node type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_WORKFLOW}.type")]
[Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_IS_NODE_KIND, NODE_SELF, false, NODE_KIND_WORKFLOW)]
public class WorkflowType : AnyType;

