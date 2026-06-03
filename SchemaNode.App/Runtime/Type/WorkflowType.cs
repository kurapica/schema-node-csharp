using System.Reflection;
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
public sealed class WorkflowType: NodeType
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
    /// The state schema type for constructor
    /// </summary>
    public string? State { get; set; }
    
    /// <summary>
    /// The session schema type
    /// </summary>
    public string? Session { get; set; }
    
    /// <summary>
    /// The workflow arguments fetch from workflow context
    /// </summary>
    public FuncArg[]? Args { get; set; } = [];
        
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
        State = workflow?.State;
        Session = workflow?.Session;
        Args = workflow?.Args;

        if (workflow == null) Status = SchemaNodeStatus.NoDefinition;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override ArrayType? GetArrayType(bool exactly = false)
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
        SchemaAttribute? typeAttr = type.GetCustomAttribute<SchemaAttribute>();
        string typeName = typeAttr?.Name ?? $"{(string.IsNullOrWhiteSpace(ns) ? "" : $"{ns}.")}{type.Name.ToLowerInvariant()}";
        NodeSchema workflowSchema = new NodeSchema
        {
            Name = typeName,
            Type = SchemaType.Workflow,
            Display = typeAttr?.Display ?? type.GetSummaryFromXmlDoc() ?? typeName,
            Workflow = new WorkflowSchema
            {
                Mode = type == typeof(EventWorkflow) || type.IsSubclassOf(typeof(EventWorkflow)) ? WorkflowMode.Event 
                    : type == typeof(FunctionWorkflow) || type.IsSubclassOf(typeof(FunctionWorkflow)) ? WorkflowMode.Function 
                        : type == typeof(InteractionWorkflow) || type.IsSubclassOf(typeof(InteractionWorkflow)) ? WorkflowMode.Interaction 
                            : WorkflowMode.Workflow,
            }
        };

        // Keep in the same namespace
        if (typeAttr?.Name != null)
            ns = string.Join('.', typeAttr.Name.Split('.', StringSplitOptions.RemoveEmptyEntries).SkipLast(1));

        // State
        Type? stateType = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IWorkflowState<>))?.GetGenericArguments()[0];
        workflowSchema.Workflow.State = stateType?.GetSchemaType(true, ns);
        
        // Session
        Type? sessionType = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IWorkflowSession<>))?.GetGenericArguments()[0];
        workflowSchema.Workflow.Session = sessionType?.GetSchemaType(true, ns);
        
        // Payload
        Type? payloadType = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IWorkflowPayload<>))?.GetGenericArguments()[0];
        workflowSchema.Workflow.Payload = payloadType?.GetSchemaType(true, ns) ?? (type.GetInterfaces().Any(i => i == typeof(IWorkflowPayload)) ? "T" : "");
        
        // Args
        MethodInfo processMethod = type.GetMethod(Workflow.WORKFLOW_PROCESS_METHOD, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new Exception($"Can't find method ProcessAsync in {type.Name}");
    
        // must be async method, the first parameter is WorkflowContext
        // the second parameter is session if any
        ParameterInfo[] parameters = processMethod.GetParameters();
        if (parameters.Length == 0 || !parameters[0].ParameterType.IsAssignableTo(typeof(WorkflowContext)))
            throw new Exception($"Invalid ProcessAsync method in workflow type {type.FullName}");

        if (sessionType != null)
        {
            if (parameters.Length < 2 || !parameters[1].ParameterType.IsAssignableTo(sessionType))
                throw new Exception($"Invalid ProcessAsync method in workflow type {type.FullName}, session parameter mismatch");
            
            // check return type
            if (!processMethod.ReturnType.IsGenericType || processMethod.ReturnType.GetGenericTypeDefinition() != typeof(Task<>) ||
                !processMethod.ReturnType.GetGenericArguments()[0].IsAssignableTo(sessionType))
            {
                throw new Exception($"Invalid ProcessAsync method in workflow type {type.FullName}, return type mismatch");
            }
        }
        
        // Gather other parameters
        parameters = parameters.Skip(sessionType != null ? 2 : 1).ToArray();
        if (parameters.Length > 0)
        {
            workflowSchema.Workflow.Args = new FuncArg[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo param = parameters[i];
                
                Utility.Schema.SchemaParamTypeInfo? info = param.ParameterType.GetSchemaTypeInfo(true, defaultNs: ns);
                if (info == null)
                    throw new Exception($"Unsupported parameter type {param.ParameterType.FullName} in ProcessAsync method of workflow type {type.FullName}");

                SchemaAttribute? attr = param.GetCustomAttribute<SchemaAttribute>();
                bool isParams = param.IsDefined(typeof(ParamArrayAttribute), false);

                workflowSchema.Workflow.Args[i] = new FuncArg
                {
                    Name = param.Name ?? $"arg{i}",
                    Type = attr?.Name 
                        ?? (isParams && info.SchemaType != null && info.SchemaType.EndsWith("s") && Utility.Schema.GetSystemNodeSchema(info.SchemaType)?.Type == SchemaType.Array ? info.SchemaType[..^1] : info.SchemaType)
                        ?? throw new Exception($"Unsupported parameter type {param.ParameterType.FullName} in ProcessAsync method of workflow type {type.FullName}"),
                    Nullable = info.Nullable || param is { HasDefaultValue: true, DefaultValue: null },
                    Params = isParams ? true : null,
                };
            }
        }

        if (SystemLocale.HasLocales)
            SystemLocale.Translate(workflowSchema.Display, workflowSchema.Name);

        return [ workflowSchema ];
    }
    
    #endregion
    
    #region Conversion
    
    /// <summary>
    /// Convert the node to schema
    /// </summary>
    public static implicit operator NodeSchema?(WorkflowType? schema)
    {
        return schema?.ToSchema().With(new WorkflowSchema
        {
            Mode = schema.WorkflowMode,
            Payload = schema.Payload,
            State = schema.State,
            Session = schema.Session,
            Args = schema.Args,
        });
    }
     
    #endregion
}