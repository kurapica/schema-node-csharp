using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using static SchemaNode.Utility.Constant;

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
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string Name { get; set; } = default!;

    /// <summary>
    /// The seqno
    /// </summary>
    public int Seqno { get; set; }
    
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
    /// The work flow node type
    /// </summary>
    [Required]
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// The workflow node return type
    /// </summary>
    public string Return { get; set; } = string.Empty;
    
    /// <summary>
    /// The function name if type is Function
    /// </summary>
    public string? Func { get; set; }
    
    /// <summary>
    /// The event name if type is Event
    /// </summary>
    public string? Event { get; set; }
    
    /// <summary>
    /// The workflow arguments
    /// </summary>
    public FuncArg[] Args { get; set; } = [];
    
    /// <summary>
    /// The state schema type for constructor
    /// </summary>
    public JsonNode? State { get; set; }
    
    /// <summary>
    /// The additional data
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
}