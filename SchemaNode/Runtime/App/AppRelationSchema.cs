using SchemaNode.Enum;
using System.Text.Json.Serialization;

namespace SchemaNode.Runtime;


/// <summary>
/// The app relation schema
/// </summary>
public class AppRelationSchema
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
    ///  The relation type
    /// </summary>
    public RelationType Type { get; init; } = RelationType.Default;

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
    public FunctionType? FunctionNode { get; set; }

    /// <summary>
    /// The relation status
    /// </summary>
    public SchemaNodeStatus Status { get; set; } = SchemaNodeStatus.Ready;
}
