using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Enum;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Schema;

/// <summary>
/// The workflow schema
/// </summary>
[SchemaApp]
public class WorkflowSchema
{
    /// <summary>
    /// The workflow name
    /// </summary>
    [Index]
    [JsonIgnore]
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? Name { get; set; }
    
    /// <summary>
    /// The workflow mode
    /// </summary>
    public WorkflowMode Mode { get; set;  }
    
    /// <summary>
    /// The workflow return type
    /// </summary>
    public string? Return { get; set; }
    
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
    public string? State { get; set; }
    
    /// <summary>
    /// The session schema type
    /// </summary>
    public string? Session { get; set; }
    
    /// <summary>
    /// The additional data
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
}