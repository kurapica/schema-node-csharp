using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Function;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Enum;
using SchemaNode.Property.Property;
using SchemaNode.Property.String;
using SchemaNode.Property.Struct;
using SchemaNode.Relation;
using SchemaNode.Runtime;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.WorkflowConstant;
using SchemaKind = SchemaNode.Property.Record.SchemaKind;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Schema;

/// <summary>
/// The workflow step
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_NODE_WORKFLOW_STEP, SCHEMA_KIND_ORDER_WORKFLOW_STEP)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_WORKFLOW}.step")]
[Meta<Append>(typeof(Display), typeof(Description))]
[Meta<Attach>(SCHEMA_KIND_NODE_WORKFLOW_STEP)]
public sealed class WorkflowStepSchema: PropertyOwner, IErrorProvider
{
    /// <summary>
    /// The node name
    /// </summary>
    [Meta<UpLimitString>(ENTITY_PRIMARY_KEY_MAX_LEN)]
    [Meta<PrimaryIndex>]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// The work flow node type
    /// </summary>
    [Meta<SchemaType>(typeof(WorkflowNodeType))]
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// The workflow node payload schema type
    /// </summary>
    [Meta<SchemaType>(typeof(ValueType))]
    [Meta<DisplayOnly>(true)]
    [Relation<Default, Call>(nameof(Payload), $"{NS_SYSTEM_SCHEMA_REFLECT_WORKFLOW}.{nameof(SystemReflectWorkflow.getpayloadtype)}", $"@{nameof(Type)}")]
    public string Payload { get; set; } = string.Empty;
    
    /// <summary>
    /// The workflow arguments
    /// </summary>
    [Relation<Visible, Call>(NODE_SELF, $"{NS_SYSTEM_SCHEMA_REFLECT_WORKFLOW}.{nameof(SystemReflectWorkflow.hasargs)}", $"@{nameof(Type)}")]
    public CallArg[]? Args { get; set; }

    /// <summary>
    /// THe previous nodes
    /// </summary>
    [Relation<CascadeDepth, Assign>($"{nameof(Previous)}.{ARRAY_ELEMENT}", 1)]
    [Relation<BlackList, Call>($"{nameof(Previous)}.{ARRAY_ELEMENT}", $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(Previous)}.{ARRAY_PREVIOUS}")]
    public string[]? Previous { get; set; }

    /// <summary>
    /// The workflow state data
    /// </summary>
    [Relation<OverrideType, Call>(nameof(State), $"{NS_SYSTEM_SCHEMA_REFLECT_WORKFLOW}.{nameof(SystemReflectWorkflow.getstatetype)}", $"@{nameof(Type)}")]
    [Relation<Visible, Call>(NODE_SELF, $"{NS_SYSTEM_SCHEMA_REFLECT_WORKFLOW}.{nameof(SystemReflectWorkflow.hasstate)}", $"@{nameof(Type)}")]
    public JsonNode? State { get; set; }

    /// <summary>
    /// The node could be triggered multiple times
    /// fork the workflow for next nodes
    /// </summary>
    [Relation<Visible, Call>(NODE_SELF, $"{NS_SYSTEM_SCHEMA_REFLECT_WORKFLOW}.{nameof(SystemReflectWorkflow.isforkable)}", $"@{nameof(Type)}")]
    public bool? Fork { get; set; }

    /// <summary>
    /// The fork key paths in the payload
    /// </summary>
    [Relation<AccessEntryConsumer, Assign>($"{nameof(ForkKey)}.{ARRAY_ELEMENT}", NS_SYSTEM_SCHEMA_REFLECT_IS_NODE_KIND, NODE_SELF, false, NODE_KIND_ENUM, NODE_KIND_STRING, NODE_KIND_INT, NODE_KIND_DECIMAL, NODE_KIND_DATE, NODE_KIND_BOOL)]
    [Relation<EntrySource, Assign>($"{nameof(ForkKey)}.{ARRAY_ELEMENT}", $"{NS_SYSTEM_SCHEMA_REFLECT_TYPE}.{nameof(Function.Reflect.Type.getaccessentries)}", $"@{nameof(Payload)}", NODE_SELF)]
    [Relation<Visible, Call>(NODE_SELF, $"{NS_SYSTEM_SCHEMA_REFLECT_WORKFLOW}.{nameof(SystemReflectWorkflow.isforkable)}", $"@{nameof(Type)}")]
    [Relation<InVisible, Call>(NODE_SELF, NS_SYSTEM_SCHEMA_REFLECT_IS_NODE_KIND, $"@{nameof(Payload)}", false, NODE_KIND_ARRAY)]
    public string[]? ForkKey { get; set; }
    
    /// <summary>
    /// The node can't be canceled
    /// </summary>
    [Relation<Visible, Call>(NODE_SELF, $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.not)}",  $"@{nameof(Fork)}")]
    public bool? UnCancelable { get; set; }
    
    /// <summary>
    /// Cancel the previous fork branches
    /// </summary>
    [Relation<Visible, Call>(NODE_SELF, $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.notempty)}",  $"@{nameof(ForkKey)}")]
    public bool? CancelPre { get; set; }
    
    /// <summary>
    /// Whether save the payload data
    /// </summary>
    [Relation<Visible, Call>(NODE_SELF, $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.notempty)}", $"@{nameof(Payload)}")]
    public bool? SavePayload { get; set; }
        
    /// <summary>
    /// The error status
    /// </summary>
    [SchemaIgnore]
    public string? Error { get; set; }
    
    /// <summary>
    /// The resolved payload schema type
    /// </summary>
    [JsonIgnore]
    [SchemaIgnore]
    public Runtime.ValueType? PayloadValueType { get; set; }
}
