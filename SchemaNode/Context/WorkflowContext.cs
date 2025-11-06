using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Components;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Utility;

namespace SchemaNode.Context;

/// <summary>
/// The workflow context
/// </summary>
public class WorkflowContext: SchemaContext, IDisposable
{
    #region Private Fields
    
    protected readonly IServiceScope Scope;
    private Workflow[] _workflows = [];
    
    #endregion
    
    #region Constructors

    public WorkflowContext(AppType app, IServiceScopeFactory scopeFactory): this(app, scopeFactory.CreateScope())
    {
    }
    
    private WorkflowContext(AppType app, IServiceScope scope): base(scope.ServiceProvider)
    {
        Application = app;
        Scope = scope;
    }
    
    #endregion
    
    #region Properties

    /// <summary>
    /// The workflow unique identifier
    /// </summary>
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    
    /// <summary>
    /// The application
    /// </summary>
    public AppType Application { get; private set; }
    
    #endregion
    
    #region Methods
    
    /// <summary>
    /// Init the workflow context with app workflow node schemas
    /// </summary>
    public async Task InitializeAsync(AppWorkflowNodeSchema[] nodeSchemas)
    {
        // init the state for nodes
        List<Workflow> topNodes = [];
        
        for (int i = 0; i < nodeSchemas.Length; i++)
        {
            var node = nodeSchemas[i];
            var workflowType = await GetSchemaTypeAsync(node.Type) as WorkflowType;
            Type csharpType = workflowType?.ToCSharpType() ?? throw new InvalidOperationException($"Workflow type {node.Type} not found");

            Workflow wNode = (Workflow)Activator.CreateInstance(csharpType)!; // All constructors parameters goto state
            wNode.Name = node.Name;
            wNode.Context = this;
            
            // state
            if (!string.IsNullOrEmpty(workflowType.State) && node.State != null && !node.State.IsEmpty())
            {
                var stateSchemaType = await GetSchemaTypeAsync(workflowType.State);
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
                        ? await GetSchemaTypeAsync(node.Func) as FunctionType
                        : null)
                    ?? throw new InvalidOperationException($"Function name is required for function workflow node {node.Name}");
                    break;
                
                case EventWorkflow evWorkflow:
                    evWorkflow.Event = (!string.IsNullOrWhiteSpace(node.Event)
                        ? await GetSchemaTypeAsync(node.Event) as EventType
                        : null)
                    ?? throw new InvalidOperationException($"Event name is required for event workflow node {node.Name}");
                    break;
            }
            
            // Relations
            if (node.Previous is { Length: > 0 })
            {
                wNode.Previous = new Workflow[node.Previous.Length];
                for (int j = 0; j < node.Previous.Length; j++)
                {
                    var prevNodeState = _workflows.FirstOrDefault(ns 
                            => ns.Name.Equals( node.Previous[j], StringComparison.OrdinalIgnoreCase));
                    if (prevNodeState == null)
                        throw new InvalidOperationException(
                            $"Previous workflow node {node.Previous[j]} not found for node {node.Name}");
                    wNode.Previous[j] = prevNodeState;
                    prevNodeState.Next ??= [];
                    prevNodeState.Next = prevNodeState.Next.Append(wNode).ToArray();
                }
            }
            else
            {
                topNodes.Add(wNode);
            }
        }
        
        // record the first nodes
        _workflows = topNodes.ToArray();
    }
    
    /// <summary>
    /// The workflow node is done with payload
    /// </summary>
    public void Done(string name, AnySchemaNode? payload = null)
    {
        Workflow? workflow = _workflows.FirstOrDefault(w => w.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (workflow == null) throw new InvalidOperationException($"Workflow node {name} not found in the context");
        Done(workflow, payload);
    }
    
    /// <summary>
    /// The workflow node is done with payload
    /// </summary>
    public void Done(Workflow workflow, AnySchemaNode? payload = null)
    {
        workflow.Status = WorkflowStatus.Done;
        workflow.Payload = payload;
    }
    
    /// <summary>
    /// The workflow node has error
    /// </summary>
    public void Error(string name, Exception exception)
    {
        Workflow? workflow = _workflows.FirstOrDefault(w => w.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (workflow == null) throw new InvalidOperationException($"Workflow node {name} not found in the context");
        Error(workflow, exception);
    }
    
    /// <summary>
    /// The workflow node has error
    /// </summary>
    public void Error(Workflow workflow, Exception exception)
    {
        workflow.Status = WorkflowStatus.Error;
        workflow.Error = exception;
    }

    /// <summary>
    /// Try process the workflow
    /// </summary>
    public void Process()
    {
    }
    
    #endregion

    #region IDisposable
    
    public void Dispose() => Scope.Dispose();

    #endregion
}