using System.Text.Json.Nodes;
using SchemaNode.Attribute;
using SchemaNode.Enum.Workflow;
using SchemaNode.Property.App;
using SchemaNode.Property.Core;
using SchemaNode.Scalar;
using SchemaNode.Workflow;
using static SchemaNode.Utility.AppConstant;
using static SchemaNode.Utility.Constant;
using Guid = System.Guid;
using Index = SchemaNode.Property.Core.Index;

namespace SchemaNode.Data.Entity;

/// <summary>
/// The workflow context snapshot storage declaration
/// </summary>
[Meta<App>(NS_SYSTEM_SCHEMA)]
[Meta<EnableStorage>(true)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP_WORKFLOW}.snapshot")]
internal class AppWorkflowContextSnapshotEntity
{
    /// <summary>
    /// The workflow name
    /// </summary>
    [Meta<PrimaryIndex>(0)]
    [Meta<SchemaType>(typeof(Identifier))]
    [Meta<Index>("IX_ROOT", 0)]
    public string Workflow { get; set; } = string.Empty;

    /// <summary>
    /// The unique identifier of the workflow context snapshot
    /// </summary>
    [Meta<PrimaryIndex>(1)]
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
    [Meta<Index>("IX_ROOT", 1)]
    public Guid RootId { get; set; }

    /// <summary>
    /// The work flow status
    /// </summary>
    [Meta<Index>("IX_ROOT", 2)]
    public WorkflowStatus Status { get; set; }

    /// <summary>
    /// The workflow node snapshots
    /// </summary>
    public AppWorkflowNodeSnapshotEntity[] Nodes { get; set; } = [];

    #region Conversion

    public static implicit operator AppWorkflowContextSnapshotEntity(WorkflowContextSnapshot snapshot) => new()
    {
        Workflow = snapshot.Workflow,
        Id = snapshot.Id,
        Start = snapshot.Start,
        CreateTime = snapshot.CreateTime,
        UpdateTime = snapshot.UpdateTime,
        RootId = snapshot.RootId,
        Status = snapshot.Status,
        Nodes = snapshot.Nodes.Select(n => (AppWorkflowNodeSnapshotEntity)n).ToArray()
    };

    public static implicit operator WorkflowContextSnapshot(AppWorkflowContextSnapshotEntity entity) => new()
    {
        Workflow = entity.Workflow,
        Id = entity.Id,
        Start = entity.Start,
        CreateTime = entity.CreateTime,
        UpdateTime = entity.UpdateTime,
        RootId = entity.RootId,
        Status = entity.Status,
        Nodes = entity.Nodes.Select(n => (WorkflowNodeSnapshot)n).ToArray()
    };

    #endregion
}

/// <summary>
/// The workflow node snapshot storage declaration
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP_WORKFLOW}.nodesnapshot")]
internal class AppWorkflowNodeSnapshotEntity
{
    /// <summary>
    /// The node name
    /// </summary>
    [Meta<PrimaryIndex>(0)]
    [Meta<SchemaType>(typeof(Identifier))]
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

    #region Conversion

    public static implicit operator AppWorkflowNodeSnapshotEntity(WorkflowNodeSnapshot snapshot) => new()
    {
        Name = snapshot.Name,
        Status = snapshot.Status,
        Payload = snapshot.Payload?.DeepClone(),
        Error = snapshot.Error,
        Session = snapshot.Session?.DeepClone()
    };

    public static implicit operator WorkflowNodeSnapshot(AppWorkflowNodeSnapshotEntity entity) => new()
    {
        Name = entity.Name,
        Status = entity.Status,
        Payload = entity.Payload?.DeepClone(),
        Error = entity.Error,
        Session = entity.Session?.DeepClone()
    };

    #endregion
}
