using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Enum;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Schema;

/// <summary>
/// The application workflow
/// </summary>
[SchemaApp]
[Schema($"{NS_SYSTEM_SCHEMA_DEF_APP_WORKFLOW}.schema")]
public sealed class AppWorkflowSchema
{
    /// <summary>
    /// the application name
    /// </summary>
    [Index]
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string App { get; set; } = string.Empty;

    /// <summary>
    /// The work flow name
    /// </summary>
    [Index]
    [StringLength(32)]
    public string Name { get; set; } = default!;
    
    /// <summary>
    /// The authentication policy
    /// </summary>
    public PolicyItem[]? Auths { get; set; }

    /// <summary>
    /// The seqno
    /// </summary>
    public int Seqno { get; set; }
    
    /// <summary>
    /// The workflow display name
    /// </summary>
    public LocaleString? Display { get; set; }
    
    /// <summary>
    /// The workflow description
    /// </summary>
    public LocaleString? Desc { get; set; }
    
    /// <summary>
    /// Active the workflow
    /// </summary>
    public bool Active { get; set; }
    
    /// <summary>
    /// The workflow nodes
    /// </summary>
    public AppWorkflowNodeSchema[] Nodes { get; set; } = [];
    
    /// <summary>
    /// The additional data
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
}

/// <summary>
/// The application workflow node
/// </summary>
[Schema($"{NS_SYSTEM_SCHEMA_DEF_APP_WORKFLOW}.node")]
public sealed class AppWorkflowNodeSchema
{
    /// <summary>
    /// The node name
    /// </summary>
    [Required]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// The workflow display name
    /// </summary>
    public LocaleString? Display { get; set; }
    
    /// <summary>
    /// The work flow node type
    /// </summary>
    [Required]
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// The workflow node payload schema type
    /// </summary>
    public string Payload { get; set; } = string.Empty;
    
    /// <summary>
    /// The workflow arguments
    /// </summary>
    public FuncCallArg[]? Args { get; set; }
    
    /// <summary>
    /// THe previous nodes
    /// </summary>
    public string[]? Previous { get; set; }
    
    /// <summary>
    /// The function name if type is Function
    /// </summary>
    public string? Func { get; set; }
    
    /// <summary>
    /// The function call arguments
    /// </summary>
    public FuncCallArg[]? FuncArgs { get; set; }
    
    /// <summary>
    /// The event name if type is Event
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
    /// The additional data
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
    
    /// <summary>
    /// The schema node status
    /// </summary>
    [NotMapped]
    public SchemaNodeStatus? Status { get; set; }
}