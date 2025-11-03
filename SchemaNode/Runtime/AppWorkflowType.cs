using System.Text.Json;
using System.Text.Json.Serialization;
using SchemaNode.Enum;
using SchemaNode.Schema;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory application workflow schema representation
/// </summary>
public class AppWorkflowType
{
    #region Properties

    /// <summary>
    /// The application name
    /// </summary>
    public string App { get; set; } = string.Empty;
    
    /// <summary>
    /// The seqno
    /// </summary>
    public int Seqno { get; set; }

    /// <summary>
    /// The workflow name
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// The workflow nodes
    /// </summary>
    public AppWorkflowNodeSchema[] Nodes { get; set; } = [];

    /// <summary>
    /// The additional data
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
    
    #endregion
    
    #region States

    public SchemaNodeStatus Status { get; set; } = SchemaNodeStatus.Ready;

    #endregion
    
    #region Conversions

    public static implicit operator AppWorkflowType(AppWorkflowSchema schema)
    {
        return new AppWorkflowType
        {
            App = schema.App,
            Name = schema.Name,
            Nodes = schema.Nodes.ToArray(),
            Additional = schema.Additional,
        };
    }

    public static implicit operator AppWorkflowSchema(AppWorkflowType type)
    {
        return new AppWorkflowSchema
        {
            App = type.App,
            Name = type.Name,
            Nodes = type.Nodes.ToArray(),
            Additional = type.Additional
        };
    }
    
    #endregion
}