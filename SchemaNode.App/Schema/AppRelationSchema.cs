using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.App.Schema;

/// <summary>
/// A function-call argument used in workflow nodes, relation rules, and union validations.
/// </summary>
public sealed class FuncCallArg
{
    /// <summary>The argument name (or expression name)</summary>
    public string? Name { get; set; }

    /// <summary>The const / literal value</summary>
    public JsonNode? Value { get; set; }

    /// <summary>Override type hint when the type cannot be inferred</summary>
    public string? Type { get; set; }

    /// <summary>Resolved schema type (populated at runtime, not serialized)</summary>
    [JsonIgnore]
    public Runtime.NodeType? SchemeType { get; set; }
}

/// <summary>
/// A struct-level relation rule attached to an AppSchema — serialised form stored in the database.
/// </summary>
public sealed class StructRelationSchema
{
    /// <summary>Target field path (may include dot-separated sub-field)</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>The property name to affect (e.g. "default")</summary>
    public string Prop { get; set; } = string.Empty;

    /// <summary>The function name that computes the value</summary>
    public string Func { get; set; } = string.Empty;

    /// <summary>The call arguments</summary>
    public FuncCallArg[] Args { get; set; } = [];
}

/// <summary>
/// The in-memory app-level relation rule (populated from StructRelationSchema after load).
/// </summary>
public sealed class AppRelationSchema
{
    /// <summary>App field name (first segment of the original Field path)</summary>
    public required string AppField { get; init; }

    /// <summary>Data field name (second segment of the original Field path, may be empty)</summary>
    public string DataField { get; init; } = string.Empty;

    /// <summary>The property name to affect</summary>
    public required string Prop { get; set; }

    /// <summary>The function name</summary>
    public required string Func { get; init; }

    /// <summary>The call arguments</summary>
    public AppArgSchema[] Args { get; init; } = [];

    /// <summary>Resolved app field (set at runtime)</summary>
    [JsonIgnore]
    public AppFieldType? FieldNode { get; set; }

    /// <summary>Resolved function node (set at runtime)</summary>
    [JsonIgnore]
    public Runtime.NodeType? FuncNode { get; set; }

    /// <summary>Load status</summary>
    public string? Status { get; set; }
}

/// <summary>
/// A single argument binding inside an AppRelationSchema.
/// </summary>
public sealed class AppArgSchema
{
    /// <summary>App field name (may be empty for const args)</summary>
    public string? AppField { get; init; }

    /// <summary>Data field name (may be empty)</summary>
    public string? DataField { get; init; }

    /// <summary>Literal value</summary>
    public JsonNode? Value { get; init; }
}
