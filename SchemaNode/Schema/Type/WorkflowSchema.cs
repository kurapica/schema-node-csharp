using SchemaNode.Attribute;
using SchemaNode.Enum;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Schema;

/// <summary>
/// The workflow schema
/// </summary>
[SchemaApp]
[Schema($"{NS_SYSTEM_SCHEMA_DEF_WORKFLOW}.schema")]
public sealed class WorkflowSchema: ISchemaExtensions
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
    public string? Payload { get; set; }
    
    /// <summary>
    /// The state schema type for creation
    /// </summary>
    public string? State { get; set; }
    
    /// <summary>
    /// The session schema type for processing
    /// </summary>
    public string? Session { get; set; }
    
    /// <summary>
    /// The workflow arguments fetch from workflow context
    /// </summary>
    public FuncArg[]? Args { get; set; } = [];

    /// <summary>
    /// The extensions
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extensions { get; set; }

}