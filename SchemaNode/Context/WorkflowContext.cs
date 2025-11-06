using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Components;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Runtime;

namespace SchemaNode.Context;

/// <summary>
/// The workflow context
/// </summary>
public class WorkflowContext: SchemaContext, IDisposable
{
    #region Private Fields
    
    protected WorkflowContext? _root;
    readonly IServiceScope _scope;
    protected Workflow? _workflow;
    readonly IWorkflowScheduler _scheduler;
    internal ConcurrentDictionary<string, WorkflowState> _states = [];
    
    #endregion
    
    #region Constructors

    public WorkflowContext(AppType app, IServiceScopeFactory scopeFactory, IWorkflowScheduler scheduler): this(app, scopeFactory.CreateScope(), scheduler)
    {
    }
    
    private WorkflowContext(AppType app, IServiceScope scope, IWorkflowScheduler scheduler) : base(scope.ServiceProvider)
    {
        Application = app;
        _scope = scope;
        _scheduler = scheduler;
    }
    
    #endregion
    
    #region Properties

    /// <summary>
    /// The workflow unique identifier
    /// </summary>
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>
    /// The root workflow context id
    /// </summary>
    public Guid? RootId => _root?.Id;

    /// <summary>
    /// The application
    /// </summary>
    public AppType Application { get; private set; }
    
    #endregion
    
    #region Methods
    
    /// <summary>
    /// Init the workflow context with app workflow node schemas
    /// </summary>
    public void Initialize(Workflow workflow, WorkflowContext? root = null)
    {
        // record the first nodes
        _root = root;
        _workflow = workflow;
        _states.Clear();

        // schedule the workflow context for processing
        if (root == null) _scheduler.Schedule(this);
    }
    
    /// <summary>
    /// The workflow node is done with payload
    /// </summary>
    public void Done(string name, AnySchemaNode? payload = null)
    {
        Workflow workflow = _workflow?.FindByName(name)
            ?? throw new InvalidOperationException($"Workflow node {name} not found in the context");

        // fork the workflow context for next nodes
        if (workflow.Fork && workflow != _workflow && workflow.Next != null && workflow.Next.Length > 0)
        {
            // Fork a new workflow context for next nodes
            WorkflowContext context = new WorkflowContext(Application, _scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>(), _scheduler);
            context.Initialize(workflow, this);
            context.Done(workflow.Name, payload);
            _scheduler.Schedule(context); // schedule the new workflow context for next processing
            return;
        }

        // record the done state
        _states[workflow.Name] = new WorkflowState
        {
            Status = WorkflowStatus.Done,
            Payload = payload
        };

        // schedule the workflow context for next processing
        _scheduler.Schedule(this); 
    }
    
    /// <summary>
    /// The workflow node is done with payload
    /// </summary>
    public void Done(Workflow workflow, AnySchemaNode? payload = null)
    {
        Done(workflow.Name, payload);
    }
    
    /// <summary>
    /// The workflow node has error
    /// </summary>
    public void Error(string name, Exception exception)
    {
        Workflow workflow = _workflow?.FindByName(name) 
            ?? throw new InvalidOperationException($"Workflow node {name} not found in the context");
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
    
    public void Dispose() => _scope.Dispose();

    #endregion

    #region Inner Type

    internal class WorkflowState
    {
        /// <summary>
        /// The workflow status
        /// </summary>
        public WorkflowStatus Status { get; set; } = WorkflowStatus.Waiting;

        /// <summary>
        /// The workflow payload
        /// </summary>
        public AnySchemaNode? Payload { get; set; }
    }

    #endregion
}