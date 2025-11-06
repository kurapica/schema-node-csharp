using System.Collections.Concurrent;
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
    public WorkflowMode WorkflowMode { get; set; } = WorkflowMode.Workflow;
    
    /// <summary>
    /// The workflow payload type
    /// </summary>
    public string? Payload { get; set; }
    
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
        WorkflowMode = workflow?.Mode ?? WorkflowMode.Workflow;
        Payload = workflow?.Payload;
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
        
        // State
        Type? stateType = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IWorkflowState<>))?.GetGenericArguments()[0];
        workflowSchema.Workflow.State = stateType?.GetSchemaType(true);
        
        // Session
        Type? sessionType = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IWorkflowSession<>))?.GetGenericArguments()[0];
        workflowSchema.Workflow.Session = sessionType?.GetSchemaType(true);
        
        // Payload
        Type? payloadType = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IWorkflowPayload<>))?.GetGenericArguments()[0];
        workflowSchema.Workflow.Payload = payloadType?.GetSchemaType(true) ?? (type.GetInterfaces().Any(i => i == typeof(IEventPayload)) ? "T" : "");
        
        // If normal workflow, need check arguments from ProcessAsync
        if (workflowSchema.Workflow.Mode != WorkflowMode.Function)
        {
            // The normal workflow should declare the arguments in ProcessAsync
            MethodInfo? processMethod = type.GetMethod(nameof(Workflow.ProcessAsync), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (processMethod != null)
            {
                ParameterInfo[] parameters = processMethod.GetParameters();
                workflowSchema.Workflow.Args = new FuncArg[parameters.Length - 1];
                if (parameters.Length > 1)
                {
                    for (int i = 1; i < parameters.Length; i++)
                    {
                        ParameterInfo param = parameters[i];
                        SchemaTypeAttribute? paramType = param.GetCustomAttribute<SchemaTypeAttribute>();
                        var info = param.ParameterType.GetSchemaTypeInfo();
                        workflowSchema.Workflow.Args[i - 1] = new FuncArg
                        {
                            Name = param.Name ?? $"arg{i}",
                            Type = paramType?.Name ?? info?.GetSchemaType(true) ?? "T",
                            Nullable = info?.Nullable
                        };
                    }
                }
            }
        }
        
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
                Payload = schema.Payload,
                Args = schema.Args,
                State = schema.State,
                Session = schema.Session,
                Additional = schema.Additional
            }
        };
    }
     
    #endregion
}