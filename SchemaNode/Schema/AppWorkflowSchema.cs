using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Schema;

/// <summary>
/// The application workflow
/// </summary>
[SchemaApp]
public class AppWorkflowSchema
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
public class AppWorkflowNodeSchema
{
    /// <summary>
    /// The node name
    /// </summary>
    [Required]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// The workflow display name
    /// </summary>
    public LocaleString? Display { get; private set; }
    
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
    /// The state schema type for constructor
    /// </summary>
    public JsonNode? State { get; set; }

    /// <summary>
    /// The node could be triggered multiple times
    /// fork the workflow for next nodes
    /// </summary>
    public bool? Fork { get; set; }
    
    /// <summary>
    /// The additional data
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
}