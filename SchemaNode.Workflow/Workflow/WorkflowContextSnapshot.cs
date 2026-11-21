using System.Text.Json.Nodes;
using SchemaNode.Enum.Workflow;
using Guid = System.Guid;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Workflow;

/// <summary>
/// The workflow context snapshot.
/// The storage declaration lives in the app layer, this type is the workflow facing data contract.
/// </summary>
public class WorkflowContextSnapshot
{
    /// <summary>
    /// The workflow name
    /// </summary>
    public string Workflow { get; set; } = string.Empty;

    /// <summary>
    /// The unique identifier of the workflow context snapshot.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The start root node
    /// </summary>
    public string Start { get; set; } = string.Empty;

    /// <summary>
    /// The creation time
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// The last update time
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// The root id
    /// </summary>
    public Guid RootId { get; set; }

    /// <summary>
    /// The work flow status
    /// </summary>
    public WorkflowStatus Status { get; set; }

    /// <summary>
    /// The workflow node snapshots
    /// </summary>
    public WorkflowNodeSnapshot[] Nodes { get; set; } = [];

    /// <summary>
    /// The fork context snapshots
    /// </summary>
    public WorkflowContextSnapshot[]? Forks { get; set; }
}

/// <summary>
/// The workflow node snapshot.
/// </summary>
public class WorkflowNodeSnapshot
{
    /// <summary>
    /// The node name
    /// </summary>
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
