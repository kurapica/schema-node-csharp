using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Schema;
using SchemaNode.Utility;

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
    public Enum.WorkflowMode WorkflowMode { get; set; } = Enum.WorkflowMode.Function;
    
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
        WorkflowMode = workflow?.Mode ?? Enum.WorkflowMode.Function;
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

    /// <summary>
    /// Generate the system workflow schemas from type
    /// </summary>
    public static NodeSchema[] GenerateSystemWorkflow(Type type, string? ns = null)
    {
        if (!type.IsAssignableTo(typeof(Workflow))) return [];
        
        // Common
        SchemaTypeAttribute? typeAttr = type.GetCustomAttribute<SchemaTypeAttribute>();
        string typeName = typeAttr?.Name ?? $"{(string.IsNullOrWhiteSpace(ns) ? "" : $"{ns}.")}{type.Name.ToLowerInvariant()}";
        NodeSchema workflowSchema = new NodeSchema
        {
            Name = typeName,
            Type = SchemaType.Workflow,
            Display = typeAttr?.Display ?? type.GetSummaryFromXmlDoc() ?? typeName,
            Workflow = new WorkflowSchema
            {
                Mode = type.IsSubclassOf(typeof(EventWorkflow)) ? WorkflowMode.Event 
                    : type.IsSubclassOf(typeof(FunctionWorkflow)) 
                        ? WorkflowMode.Function 
                        : WorkflowMode.Workflow,
            }
        };
        
        return [ workflowSchema ];
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
                Mode = schema.WorkflowMode,
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