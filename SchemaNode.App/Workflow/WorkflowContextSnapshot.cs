using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Nodes;
using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Property.App;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Scalar;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;
using Guid = System.Guid;
using Index = SchemaNode.Property.Core.Index;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Workflow;

/// <summary>
/// The workflow context snapshot.
/// </summary>
[Meta<App>($"{NS_SYSTEM_SCHEMA}")]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP_WORKFLOW}.snapshot")]
public class WorkflowContextSnapshot
{
    /// <summary>
    /// The application
    /// </summary>
    [Meta<PrimaryIndex>(0)]
    [Meta<SchemaType>(typeof(AppType))]
    [Meta<Index>("IX_ROOT",0)]
    public string App { get; set; } = string.Empty;

    /// <summary>
    /// The workflow name
    /// </summary>
    [Meta<PrimaryIndex>(1)]
    [Meta<SchemaType>(typeof(Identifier))]
    [Meta<Index>("IX_ROOT",1)]
    public string Workflow { get; set; } = string.Empty;
    
    /// <summary>
    /// The unique identifier of the workflow context snapshot.
    /// </summary>
    [Meta<PrimaryIndex>(2)]
    [Meta<Index>("IX_ROOT",4)]
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
    [Meta<Index>("IX_ROOT",2)]
    public Guid RootId { get; set; }
    
    /// <summary>
    /// The work flow status
    /// </summary>
    [Meta<Index>("IX_ROOT",3)]
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

[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP_WORKFLOW}.nodesnapshot")]
public class WorkflowNodeSnapshot
{
    /// <summary>
    /// The node name
    /// </summary>
    [Meta<PrimaryIndex>(0)]
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