using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SchemaNode.Schema;
using SchemaNode.Struct;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.App.Schema;

/// <summary>
/// The application workflow schema — describes a named workflow attached to an application.
/// </summary>
public sealed class AppWorkflowSchema
{
    /// <summary>Application name (set by the loader)</summary>
    public string App { get; set; } = string.Empty;

    /// <summary>Workflow name (unique within the application)</summary>
    public string Name { get; set; } = default!;

    /// <summary>Display order</summary>
    public int Seqno { get; set; }

    /// <summary>Display name</summary>
    public LocaleString? Display { get; set; }

    /// <summary>Inline authentication policies</summary>
    public PolicyItem[]? Auths { get; set; }

    /// <summary>Whether the workflow is activated and should be kept running</summary>
    public bool Active { get; set; }

    /// <summary>Ordered list of workflow nodes</summary>
    public AppWorkflowNodeSchema[] Nodes { get; set; } = [];

    /// <summary>Extension data</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extensions { get; set; }
}

/// <summary>
/// A single node in a workflow definition.
/// </summary>
public sealed class AppWorkflowNodeSchema
{
    /// <summary>Node name (unique within the workflow)</summary>
    public required string Name { get; set; }

    /// <summary>Display name</summary>
    public LocaleString? Display { get; set; }

    /// <summary>Workflow node type schema name (resolves to a WorkflowType)</summary>
    public required string Type { get; set; }

    /// <summary>Payload schema type name</summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>Arguments for the workflow node type</summary>
    public FuncCallArg[]? Args { get; set; }

    /// <summary>Previous node names (DAG edges)</summary>
    public string[]? Previous { get; set; }

    /// <summary>Function name for FunctionWorkflow nodes</summary>
    public string? Func { get; set; }

    /// <summary>Arguments for the function call inside a FunctionWorkflow node</summary>
    public FuncCallArg[]? FuncArgs { get; set; }

    /// <summary>Event name for EventWorkflow nodes</summary>
    public string? Event { get; set; }

    /// <summary>Static state data passed to the workflow node instance</summary>
    public JsonNode? State { get; set; }

    /// <summary>If true, the node forks — one workflow branch per iteration</summary>
    public bool? Fork { get; set; }

    /// <summary>Key paths inside the payload used to determine the fork identity</summary>
    public string[]? ForkKey { get; set; }

    /// <summary>If true, the node cannot be cancelled once started</summary>
    public bool? UnCancelable { get; set; }

    /// <summary>If true, any previously forked branches are cancelled when this node is reached</summary>
    public bool? CancelPre { get; set; }

    /// <summary>Whether the payload data should be persisted</summary>
    public bool? PayloadSave { get; set; }

    /// <summary>Extension data</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extensions { get; set; }

    /// <summary>Resolved payload schema type (set at runtime, not serialised)</summary>
    [JsonIgnore]
    public Runtime.NodeType? PayloadSchemaType { get; set; }
}
