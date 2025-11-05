using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Schema;
using SchemaNode.Utility;

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
    /// Active the workflow
    /// </summary>
    public bool Active { get; set; }
    
    /// <summary>
    /// The workflow nodes
    /// </summary>
    public AppWorkflowNodeSchema[] Nodes { get; set; } = [];
    
    /// <summary>
    /// The workflow nodes
    /// </summary>
    public List<Workflow> Workflows { get; set; } = [];

    /// <summary>
    /// The additional data
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
    
    #endregion
    
    #region States

    public SchemaNodeStatus Status { get; set; } = SchemaNodeStatus.Ready;

    #endregion

    #region Relationship

    /// <summary>
    /// The application
    /// </summary>
    public AppType Application { get; set; } = null!;
    
    #endregion
    
    #region Methods
    
    /// <summary>
    /// Load the workflow schema
    /// </summary>
    public async Task LoadAsync(SchemaContext context)
    {
        // TODO: restore the saved workflow contexts
        
        // Init the workflow nodes
        foreach (var node in Nodes)
        {
            var schemaType = await context.GetSchemaTypeAsync(node.Type);
            if (schemaType is not WorkflowType workflowType) continue;
            
            Type workFlowType = schemaType.ToCSharpType();
            Workflow workflow = (Workflow)Activator.CreateInstance(workFlowType)!; // no constructor parameters
            
            // state
            if (!string.IsNullOrEmpty(workflowType.State) && node.State != null && !node.State.IsEmpty())
            {
                var stateSchemaType = await context.GetSchemaTypeAsync(workflowType.State);
                var stateType = stateSchemaType?.ToCSharpType();
                if (stateType != null)
                {
                    var workflowStateInterface = typeof(IWorkflowState<>).MakeGenericType(stateType);
                    if (workflowStateInterface.IsAssignableFrom(workFlowType))
                    {
                        workflowStateInterface.GetProperty("State")!.SetValue(workflow, stateType.TryConvert(node.State));
                    }
                }
            }

            // mode
            switch (workflow)
            {
                // Function Workflow
                case FunctionWorkflow funcWorkflow:
                {
                    if (await context.GetSchemaTypeAsync(node.Func ?? string.Empty) is FunctionType functionType)
                        funcWorkflow.Function = functionType;
                    else
                        Status = SchemaNodeStatus.WorkflowWrongFunc;
                    break;
                }
                // Event Workflow
                case EventWorkflow evWorkflow:
                {
                    if (await context.GetSchemaTypeAsync(node.Event ?? string.Empty) is EventType eventType)
                        evWorkflow.Event = eventType;
                    else
                        Status = SchemaNodeStatus.WorkflowWrongEvent;
                    break;
                }
            }

            Workflows.Add(workflow);
        }
        
        // Init the first node subscription, for now only application scope event is supported
        if (Status == SchemaNodeStatus.Ready && Active && Workflows.Count > 0 
            && Workflows[0] is EventWorkflow { Event.Scope: EventScope.Application } eventWorkflow)
        {
            
        }
    }
    
    #endregion
    
    #region Conversions

    public static implicit operator AppWorkflowType(AppWorkflowSchema schema)
    {
        return new AppWorkflowType
        {
            App = schema.App,
            Name = schema.Name,
            Seqno = schema.Seqno,
            Active = schema.Active,
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
            Seqno = type.Seqno,
            Active = type.Active,
            Nodes = type.Nodes.ToArray(),
            Additional = type.Additional
        };
    }
    
    #endregion
}