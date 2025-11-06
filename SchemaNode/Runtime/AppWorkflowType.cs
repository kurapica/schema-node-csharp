using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory application workflow schema representation
/// </summary>
public class AppWorkflowType: IDisposable
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
        
        // Init the entry workflow context
        if (Nodes.Length <= 1 || !Active) return;

        // init the state for nodes
        List<Workflow> topNodes = [];
        Dictionary<string, Workflow> _workflows = [];

        for (int i = 0; i < Nodes.Length; i++)
        {
            var node = Nodes[i];
            var workflowType = await context.GetSchemaTypeAsync(node.Type) as WorkflowType;
            Type csharpType = workflowType?.ToCSharpType() ?? throw new InvalidOperationException($"Workflow type {node.Type} not found");

            Workflow wNode = (Workflow)Activator.CreateInstance(csharpType)!; // All constructors parameters goto state
            wNode.Name = node.Name;
            wNode.Fork = node.Fork ?? false;

            // state
            if (!string.IsNullOrEmpty(workflowType.State) && node.State != null && !node.State.IsEmpty())
            {
                var stateSchemaType = await context.GetSchemaTypeAsync(workflowType.State);
                var stateType = stateSchemaType?.ToCSharpType();
                if (stateType != null)
                {
                    csharpType.GetProperty("State", BindingFlags.Public | BindingFlags.Instance)
                        ?.SetValue(wNode, stateType.TryConvert(node.State));
                }
            }

            // details
            switch (wNode)
            {
                case FunctionWorkflow funcWorkflow:
                    funcWorkflow.Function = (!string.IsNullOrWhiteSpace(node.Func)
                        ? await context.GetSchemaTypeAsync(node.Func) as FunctionType
                        : null)
                    ?? throw new InvalidOperationException($"Function name is required for function workflow node {node.Name}");
                    break;

                case EventWorkflow evWorkflow:
                    evWorkflow.Event = (!string.IsNullOrWhiteSpace(node.Event)
                        ? await context.GetSchemaTypeAsync(node.Event) as EventType
                        : null)
                    ?? throw new InvalidOperationException($"Event name is required for event workflow node {node.Name}");
                    break;
            }

            _workflows.Add(wNode.Name, wNode);

            // Relations
            if (node.Previous is { Length: > 0 })
            {
                wNode.Previous = new Workflow[node.Previous.Length];
                for (int j = 0; j < node.Previous.Length; j++)
                {
                    var prevNode = _workflows[node.Previous[j]] 
                        ?? throw new InvalidOperationException($"Previous workflow node {node.Previous[j]} not found for node {node.Name}");
                    wNode.Previous[j] = prevNode;
                    prevNode.Next ??= [];
                    prevNode.Next = prevNode.Next.Append(wNode).ToArray();
                }
            }
            else
            {
                topNodes.Add(wNode);
            }
        }

        entryContext?.Dispose();
        entryContext = ActivatorUtilities.CreateInstance<WorkflowContext>(context.ServiceProvider, this);
        entryContext.Initialize(topNodes.First());
    }

    public void Dispose()
    {
        entryContext?.Dispose();
    }

    private WorkflowContext? entryContext;

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