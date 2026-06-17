using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Property.Common;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Core;
using SchemaNode.Struct;
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
[Meta<Append>(typeof(Display))]
public sealed class AppWorkflowSchema: ExtensibleSchema
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
    [Meta<UplimitString>(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string Name { get; set; } = default!;
    
    /// <summary>
    /// The seqno
    /// </summary>
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
[Meta<Append>(typeof(Display))]
public sealed class AppWorkflowNodeSchema: ExtensibleSchema
{
    /// <summary>
    /// The node name
    /// </summary>
    [Meta<UplimitString>(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// The work flow node type
    /// </summary>
    [Meta<SchemaType>(typeof(WorkflowType))]
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// The workflow node payload schema type
    /// </summary>
    public string Payload { get; set; } = string.Empty;
    
    /// <summary>
    /// The workflow arguments
    /// </summary>
    public CallArg[]? Args { get; set; }
    
    /// <summary>
    /// THe previous nodes
    /// </summary>
    public string[]? Previous { get; set; }
    
    /// <summary>
    /// The event name if type is BaseEvent
    /// </summary>
    public string? Event { get; set; }
    
    /// <summary>
    /// The workflow state data
    /// </summary>
    public JsonNode? State { get; set; }

    /// <summary>
    /// The node could be triggered multiple times
    /// fork the workflow for next nodes
    /// </summary>
    public bool? Fork { get; set; }

    /// <summary>
    /// The fork key paths in the payload
    /// </summary>
    public string[]? ForkKey { get; set; }
    
    /// <summary>
    /// The node can't be canceled
    /// </summary>
    public bool? UnCancelable { get; set; }
    
    /// <summary>
    /// Cancel the previous fork branches
    /// </summary>
    public bool? CancelPre { get; set; }
    
    /// <summary>
    /// Whether save the payload data
    /// </summary>
    public bool? PayloadSave { get; set; }

    /// <summary>
    /// The resolved payload schema type
    /// </summary>
    [JsonIgnore]
    [SchemaIgnore]
    public ValueType? PayloadSchemaType { get; set; }
}