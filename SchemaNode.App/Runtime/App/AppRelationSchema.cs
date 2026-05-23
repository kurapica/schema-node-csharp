using SchemaNode.Attribute;
using SchemaNode.Enum;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

/// <summary>
/// The app relation schema
/// </summary>
public sealed class AppRelationSchema
{
    /// <summary>
    /// The application field
    /// </summary>
    public required string AppField { get; init; }

    /// <summary>
    /// The data field
    /// </summary>
    public string DataField { get; init; } = string.Empty;

    /// <summary>
    /// The property of the realtion, so the function can modify it dynamically
    /// </summary>
    [Schema(NS_SYSTEM_SCHEMA_PROPERTY)]
    public required string Prop { get; set; }

    /// <summary>
    /// The function name
    /// </summary>
    public required string Func { get; init; }

    /// <summary>
    /// The function arguments
    /// </summary>
    public AppArgSchema[] Args { get; init; } = [];

    /// <summary>
    /// The field node
    /// </summary>
    [JsonIgnore]
    public AppFieldType? FieldNode { get; set; }

    /// <summary>
    /// The function node
    /// </summary>
    [JsonIgnore]
    public FunctionType? FuncNode { get; set; }

    /// <summary>
    /// The relation status
    /// </summary>
    public SchemaNodeStatus Status { get; set; } = SchemaNodeStatus.Ready;
}

/// <summary>
/// The app argument
/// </summary>
public sealed class AppArgSchema
{
    /// <summary>
    /// The application field
    /// </summary>
    public string? AppField { get; init; }

    /// <summary>
    /// The data field
    /// </summary>
    public string? DataField { get; init; }

    /// <summary>
    /// The json value
    /// </summary>
    public JsonNode? Value { get; init; }
}