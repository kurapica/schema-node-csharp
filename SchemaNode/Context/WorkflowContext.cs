using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Components;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Utility;

namespace SchemaNode.Context;

/// <summary>
/// The workflow context
/// </summary>
public class WorkflowContext: SchemaContext, IDisposable
{
    #region Private Fields
    
    private WorkflowContext? _root;
    private Workflow? _workflow;
    private readonly IServiceScope _scope;
    private readonly IWorkflowScheduler _scheduler;
    private readonly ConcurrentDictionary<string, WorkflowState> _states = [];
    
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
    public AppType Application { get; }
    
    #endregion

    #region Backup & Restore

    /// <summary>
    /// Backup the workflow context state
    /// TODO
    /// </summary>
    public JsonNode? Backup()
    {
        return null;
    }

    /// <summary>
    /// Restore the workflow context from backup
    /// TODO
    /// </summary>
    public void Restore(JsonNode? backup)
    {
    }

    #endregion
    
    #region Methods
    
    /// <summary>
    /// Init the workflow context with app workflow node schemas
    /// </summary>
    internal void Initialize(Workflow workflow, WorkflowContext? root = null)
    {
        // init the workflow context
        _workflow = workflow;
        _root = root;
        _states.Clear();

        // schedule the workflow context for processing
        // if root exists, the root will schedule this context
        if (root == null) _scheduler.Schedule(this);
    }
    
    /// <summary>
    /// Gets the payload by name
    /// </summary>
    public AnySchemaNode? GetWorkflowPayload(string name)
    {
        return _states.TryGetValue(name, out WorkflowState? state) ? state.Payload : _root?.GetWorkflowPayload(name);
    }
    
    /// <summary>
    /// Gets the payload by workflow
    /// </summary>
    public AnySchemaNode? GetWorkflowPayload(Workflow workflow)
    {
        return GetWorkflowPayload(workflow.Name);
    }
    
    /// <summary>
    /// Gets the workflow status by name
    /// </summary>
    public WorkflowStatus GetWorkflowStatus(string name)
    {
        return (_states.TryGetValue(name, out WorkflowState? state) ? state.Status : _root?.GetWorkflowStatus(name))
            ?? WorkflowStatus.Waiting;
    }
    
    /// <summary>
    /// Gets teh workflow status by workflow
    /// </summary>
    public WorkflowStatus GetWorkflowStatus(Workflow workflow)
    {
        return GetWorkflowStatus(workflow.Name);
    }
    
    /// <summary>
    /// The workflow node is done with payload
    /// </summary>
    public void Done(string name, AnySchemaNode? payload = null)
    {
        Workflow workflow = _workflow?.FindByName(name)
            ?? throw new InvalidOperationException($"Workflow node {name} not found in the context");

        // get or create the workflow state
        WorkflowState state = GetOrCreateWorkflowState(name);
        
        // fork the workflow context for next nodes
        if (workflow.Fork && workflow != _workflow && workflow.Next is { Length: > 0 })
        {
            // Fork a new workflow context for next nodes
            WorkflowContext context = new WorkflowContext(Application, 
                _scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>(), 
                _scheduler);
            context.Initialize(workflow, this);
            context.Done(workflow.Name, payload);

            state.ForkContexts ??= new  ConcurrentDictionary<WorkflowContext, Guid>();
            state.ForkContexts[context] = context.Id; // record the forked context
            _scheduler.Schedule(context); // schedule the new workflow context for next processing
            return;
        }

        // record the done state
        state.Status = WorkflowStatus.Done;
        state.Payload = payload;

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
    public void Error(string name, string exception)
    {
        WorkflowState state = GetOrCreateWorkflowState(name);
        state.Status = WorkflowStatus.Error;
        state.Error = exception;
    }
    
    /// <summary>
    /// The workflow node has error
    /// </summary>
    public void Error(string name, Exception exception)
    {
        Error(name, exception.GetInnermostException().Message);
    }

    /// <summary>
    /// The workflow node has error
    /// </summary>
    public void Error(Workflow workflow, string exception)
    {
        Error(workflow.Name, exception);
    }

    /// <summary>
    /// The workflow node has error
    /// </summary>
    public void Error(Workflow workflow, Exception exception)
    {
        Error(workflow.Name, exception.GetInnermostException().Message);
    }

    /// <summary>
    /// Goto the workflow node by name
    /// </summary>
    public void Goto(string name, string? newName = null)
    {
        Workflow workflow = _workflow?.FindByName(name)
            ?? throw new InvalidOperationException($"Workflow node {name} not found in the context");
        Workflow? newWorkflow = newName != null
            ? _workflow?.FindByName(newName)
              ?? throw new InvalidOperationException($"Workflow node {newName} not found in the context")
            : null;
        
        var state = GetOrCreateWorkflowState(name);
        state.Status = WorkflowStatus.Done;

        if (newWorkflow != null)
        {
            // reset the workflow states from the target node
            void ResetWorkflowState(Workflow wf)
            {
                if (_states.TryGetValue(wf.Name, out WorkflowState? s))
                {
                    s.Status = WorkflowStatus.Waiting;
                    s.Error = null;
                }

                if (wf.Next == null) return;
                foreach (var next in wf.Next)
                    ResetWorkflowState(next);
            }

            ResetWorkflowState(newWorkflow);
        }

        // schedule the workflow context for processing
        _scheduler.Schedule(this);
    }

    /// <summary>
    /// Goto the workflow node
    /// </summary>
    public void Goto(Workflow workflow, string? newName = null)
    {
        Goto(workflow.Name, newName);
    }
    
    /// <summary>
    /// Try process the workflow
    /// </summary>
    public async Task ProcessAsync()
    {
        if (_workflow == null) return;
        
        // Find the next workflow nodes to process
        var next = GetNextWorkflowToProcess(_workflow);
        if (next == null)
        {
            // All done
            if (IsWorkflowTerminatable(_workflow))
                Terminate();
            return;
        }
        
        Workflow workflow = next.Value.Item1;
        WorkflowState state = next.Value.Item2;
        try
        {
            // Process the workflow
            state.Status = WorkflowStatus.Running;
            await state.ProcessAsync(this, workflow);
        }
        catch (Exception ex)
        {
            // Mark error
            state.Status = WorkflowStatus.Error;
            state.Error = ex.GetInnermostException().Message;
        }
    }

    /// <summary>
    /// Terminate the workflow context
    /// </summary>
    public void Terminate()
    {
        _workflow = null;
        Dispose();
    }
    
    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_root != null)
        {
            // remove from root fork contexts
            if (_root._states.TryGetValue(_workflow!.Name, out WorkflowState? state) && state.ForkContexts != null)
            {
                state.ForkContexts.TryRemove(this, out _);
            }
        }
        _scope.Dispose();
    }

    #endregion

    #region Inner Type

    /// <summary>
    /// The workflow state
    /// </summary>
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
        
        /// <summary>
        /// The workflow error
        /// </summary>
        public string? Error { get; set; }
        
        /// <summary>
        /// The forked workflow contexts
        /// </summary>
        public ConcurrentDictionary<WorkflowContext, Guid>? ForkContexts { get; set; }
        
        /// <summary>
        /// Has session
        /// </summary>
        public virtual bool HasSession => false;

        /// <summary>
        /// Process the workflow without session
        /// </summary>
        public virtual Task ProcessAsync(WorkflowContext context, Workflow workflow)
        {
            return workflow.ProcessAsync(context);
        }
    }

    /// <summary>
    ///  The workflow state with session
    /// </summary>
    internal class WorkflowState<T>: WorkflowState
    {
        public override bool HasSession => true;

        /// <summary>
        /// Process the workflow with session
        /// </summary>
        public override async Task ProcessAsync(WorkflowContext context, Workflow workflow)
        {
            Session = await ((IWorkflowSession<T>)workflow).ProcessAsync(context, Session);
        }
        
        /// <summary>
        /// The session
        /// </summary>
        public T Session { get; set; } = default!;
    }

    #endregion

    #region Utility

    /// <summary>
    /// Gets the next workflow to process
    /// </summary>
    /// <returns></returns>
    (Workflow, WorkflowState)? GetNextWorkflowToProcess(Workflow workflow)
    {
        WorkflowState state = _states.GetOrAdd(workflow.Name, new WorkflowState());
        if (state.Status == WorkflowStatus.Waiting)
        {
            // check previous
            if (workflow.Previous != null && 
                workflow.Previous.Select(prev => _states.GetOrAdd(prev.Name, new WorkflowState()))
                    .Any(prevState => prevState.Status != WorkflowStatus.Done))
            {
                return null; // previous not done yet
            }
            
            return (workflow, state);
        }

        if (state.Status == WorkflowStatus.Done && workflow.Next != null)
        {
            foreach (var next in workflow.Next)
            {
                var result = GetNextWorkflowToProcess(next);
                if (result != null) return result;
            }
        }
        
        // means all processed
        return null;
    }
    
    /// <summary>
    /// Check if the workflow is terminatable
    /// </summary>
    bool IsWorkflowTerminatable(Workflow workflow)
    {
        if (_states.TryGetValue(workflow.Name, out WorkflowState? state))
        {
            switch (state.Status)
            {
                case WorkflowStatus.Error:
                    return true;
                case WorkflowStatus.Waiting or WorkflowStatus.Running:
                    return false;
            }
        }
        else
        {
            return false;
        }

        return workflow.Next == null || workflow.Next.All(IsWorkflowTerminatable);
    }
    
    /// <summary>
    /// Gets or create the workflow state
    /// </summary>
    WorkflowState GetOrCreateWorkflowState(string name)
    {
        Workflow workflow = _workflow?.FindByName(name)
            ?? throw new InvalidOperationException($"Workflow node {name} not found in the context");
        return _states.GetOrAdd(_workflow!.Name, (_) =>
        {
            Type workflowType = workflow.GetType();
            if (workflowType.IsGenericType && workflowType.GetGenericTypeDefinition() == typeof(IWorkflowSession<>))
            {
                Type sessionType = workflowType.GetGenericArguments()[0];
                Type stateType = typeof(WorkflowState<>).MakeGenericType(sessionType);
                return (WorkflowState)Activator.CreateInstance(stateType)!;
            }
            return new WorkflowState();
        });
    }
    
    #endregion
}