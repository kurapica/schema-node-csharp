using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Function;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.String;
using SchemaNode.Relation;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;
using SchemaKind = SchemaNode.Property.Record.SchemaKind;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Schema;

/// <summary>
/// The application workflow
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_APP_WORKFLOW, SCHEMA_KIND_ORDER_APP_WORKFLOW)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP_WORKFLOW}.schema")]
[Meta<Append>(typeof(Display), typeof(Description))]
[Meta<Attach>(SCHEMA_KIND_APP_WORKFLOW)]
public sealed class AppWorkflowSchema: PropertyOwner
{
    /// <summary>
    /// the application name
    /// </summary>
    [Meta<PrimaryIndex>(0)]
    [Meta<SchemaType>(typeof(AppType))]
    public string App { get; set; } = string.Empty;

    /// <summary>
    /// The work flow name
    /// </summary>
    [Meta<PrimaryIndex>(1)]
    [Meta<UpLimitString>(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string Name { get; set; } = default!;
    
    /// <summary>
    /// The seqno
    /// </summary>
    [SchemaIgnore]
    [JsonIgnore]
    public int Seqno { get; set; }
    
    /// <summary>
    /// Active the workflow
    /// </summary>
    public bool Active { get; set; }
    
    /// <summary>
    /// The workflow nodes
    /// </summary>
    public AppWorkflowNodeSchema[] Nodes { get; set; } = [];
}

/// <summary>
/// The application workflow node
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_APP_WORKFLOW_NODE, SCHEMA_KIND_ORDER_APP_WORKFLOW_NODE)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP_WORKFLOW}.node")]
[Meta<Append>(typeof(Display), typeof(Description))]
[Meta<Attach>(SCHEMA_KIND_APP_WORKFLOW_NODE)]
public sealed class AppWorkflowNodeSchema: PropertyOwner, IErrorProvider
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
    [Meta<SchemaType>(typeof(WorkflowType))]
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// The workflow node payload schema type
    /// </summary>
    [Meta<SchemaType>(typeof(ValueType))]
    public string Payload { get; set; } = string.Empty;
    
    /// <summary>
    /// The workflow arguments
    /// </summary>
    [Relation<Visible, Call>(NODE_SELF, $"{NS_SYSTEM_SCHEMA_REFLECT_WORKFLOW}.{nameof(SystemReflectWorkflow.hasargs)}", $"@{nameof(Type)}")]
    public CallArg[]? Args { get; set; }
    
    /// <summary>
    /// THe previous nodes
    /// </summary>
    public string[]? Previous { get; set; }
    
    /// <summary>
    /// The workflow state data
    /// </summary>
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
    [Meta<EntrySource>($"{NS_SYSTEM_SCHEMA_REFLECT_TYPE}.{nameof(SchemaNode.Function.Reflect.Type.getaccessentries)}", $"@{nameof(Payload)}")]
    [Meta<AccessEntryConsumer>(NS_SYSTEM_SCHEMA_REFLECT_IS_SCHEMA_KIND, NODE_SELF, false, SCHEMA_KIND_ENUM, SCHEMA_KIND_STRING, SCHEMA_KIND_INT, SCHEMA_KIND_DECIMAL, SCHEMA_KIND_DATE, SCHEMA_KIND_BOOL)]
    [Relation<Visible, Call>(NODE_SELF, $"{NS_SYSTEM_SCHEMA_REFLECT_WORKFLOW}.{nameof(SystemReflectWorkflow.isforkable)}", $"@{nameof(Type)}")]
    [Relation<InVisible, Call>(NODE_SELF, NS_SYSTEM_SCHEMA_REFLECT_IS_SCHEMA_KIND, $"@{nameof(Payload)}", false, SCHEMA_KIND_ARRAY)]
    public string[]? ForkKey { get; set; }
    
    /// <summary>
    /// The node can't be canceled
    /// </summary>
    [Relation<InVisible, Call>(NODE_SELF, $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}",  $"@{nameof(Fork)}")]
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
    [Meta<SchemaType>(typeof(ErrorCode))]
    [Meta<ReadOnly>(true)]
    public string? Error { get; set; }
    
    /// <summary>
    /// The resolved payload schema type
    /// </summary>
    [JsonIgnore]
    [SchemaIgnore]
    public Runtime.ValueType? PayloadValueType { get; set; }
}