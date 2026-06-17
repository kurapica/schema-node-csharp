using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Nodes;
using SchemaNode.Attribute;
using SchemaNode.Enum;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Workflow;

/// <summary>
/// The workflow context snapshot.
/// </summary>
[SchemaApp]
[Schema($"{NS_SYSTEM_SCHEMA_DEF_APP_WORKFLOW}.snapshot")]
public class WorkflowContextSnapshot
{
    /// <summary>
    /// The application
    /// </summary>
    [NotMapped]
    public string App { get; set; } = string.Empty;

    /// <summary>
    /// The workflow name
    /// </summary>
    [Index]
    [Index("IX_ROOT")]
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string Workflow { get; set; } = string.Empty;
    
    /// <summary>
    /// The unique identifier of the workflow context snapshot.
    /// </summary>
    [Index]
    [Index("IX_ROOT", 3)]
    public Guid Id { get; set; }
    
    /// <summary>
    /// The start root node
    /// </summary>
    public string Start { get; set; } = string.Empty;
    
    /// <summary>
    /// The creation time
    /// </summary>
    public DateTime CreateTime { get; set; }
    
    /// <summary>
    /// The last update time
    /// </summary>
    public DateTime UpdateTime { get; set; }

    /// <summary>
    /// The root id
    /// </summary>
    [Index("IX_ROOT", 1)]
    public Guid RootId { get; set; }
    
    /// <summary>
    /// The work flow status
    /// </summary>
    [Index("IX_ROOT", 2)]
    public WorkflowStatus Status { get; set; }
    
    /// <summary>
    /// The workflow node snapshots
    /// </summary>
    public WorkflowNodeSnapshot[] Nodes { get; set; } = [];
    
    /// <summary>
    /// The fork context snapshots
    /// </summary>
    [NotMapped]
    public WorkflowContextSnapshot[]? Forks { get; set; }
}

[Schema($"{NS_SYSTEM_SCHEMA_DEF_APP_WORKFLOW}.nodesnapshot")]
public class WorkflowNodeSnapshot
{
    /// <summary>
    /// The node name
    /// </summary>
    [Index]
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// The workflow status
    /// </summary>
    public WorkflowStatus Status { get; set; }
    
    /// <summary>
    /// The payload
    /// </summary>
    public JsonNode? Payload { get; set; }
    
    /// <summary>
    /// The error
    /// </summary>
    public string? Error { get; set; }
    
    /// <summary>
    /// The session
    /// </summary>
    public JsonNode? Session { get; set; }
}