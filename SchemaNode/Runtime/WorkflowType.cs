using System.Text.Json;
using System.Text.Json.Serialization;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Schema;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory workflow schema representation
/// </summary>
public class WorkflowType: AnySchemeType
{
    #region Data

    /// <summary>
    /// The workflow type
    /// </summary>
    public Workflow Workflow { get; set; } = Workflow.Function;
    
    /// <summary>
    /// The workflow return type
    /// </summary>
    public string? Return { get; set; }
    
    /// <summary>
    /// The function name if type is Function
    /// </summary>
    public string? Func { get; set; }
    
    /// <summary>
    /// The event name if type is Event
    /// </summary>
    public string? Event { get; set; }
    
    /// <summary>
    /// The workflow arguments
    /// </summary>
    public FuncArg[] Args { get; set; } = [];
    
    /// <summary>
    /// The state schema type for constructor
    /// </summary>
    public string? State { get; set; }
    
    /// <summary>
    /// The session schema type
    /// </summary>
    public string? Session { get; set; }
    
    /// <summary>
    /// The additional data
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
    
    #endregion
    
    #region Status
    
    /// <inheritdoc />
    public override SchemaType Type => SchemaType.Workflow;
    
    #endregion
    
    #region Method

    /// <inheritdoc />
    public override Task LoadAsync(SchemaContext context, NodeSchema schema, bool preload = false)
    {
        WorkflowSchema? workflow = schema.Workflow;
        
        // Data
        Workflow = workflow?.Workflow ?? Enum.Workflow.Function;
        Return = workflow?.Return;
        Func = workflow?.Func;
        Event = workflow?.Event;
        Args = workflow?.Args ?? [];
        State = workflow?.State;
        Session = workflow?.Session;
        Additional = workflow?.Additional;

        if (workflow == null) Status = SchemaNodeStatus.NoDefinition;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override ArrayType? GetArrayNode(bool exactly = false)
    {
        return null;
    }

    #endregion
    
    #region Static Feature

    public static NodeSchema[] GenerateSystemStruct(Type type, string? ns = null)
    {
        return [];
    }
    
    #endregion
    
    #region Conversion
    
    /// <summary>
    /// Convert the node to schema
    /// </summary>
    public static implicit operator NodeSchema?(WorkflowType? schema)
    {
        if (schema == null) return null;
        return new NodeSchema
        {
            Name = schema.Name,
            Type = schema.Type,
            Display = schema.Display,
            LoadState = schema.LoadState,
            Used = schema.IsUsed,
            Workflow = new WorkflowSchema
            {
                Workflow = schema.Workflow,
                Return = schema.Return,
                Func = schema.Func,
                Event = schema.Event,
                Args = schema.Args,
                State = schema.State,
                Session = schema.Session,
                Additional = schema.Additional
            }
        };
    }
     
    #endregion
}