using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchemaNode.Components;
using SchemaNode.Components.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Utility;
// ReSharper disable UnusedAutoPropertyAccessor.Global

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

    public WorkflowContext(IServiceScopeFactory scopeFactory, IWorkflowScheduler scheduler): this(scopeFactory.CreateScope(), scheduler)
    {
    }
    
    private WorkflowContext(IServiceScope scope, IWorkflowScheduler scheduler) : base(scope.ServiceProvider)
    {
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
    /// The workflow
    /// </summary>
    public AppWorkflowType Workflow { get; private set; }
    
    #endregion

    #region Backup & Restore

    /// <summary>
    /// Backup the workflow context state
    /// </summary>
    public WorkflowContextSnapshot? Backup(bool forks = false)
    {
        return _workflow != null 
            ? new WorkflowContextSnapshot
                {
                    App = Workflow.App,
                    Workflow = Workflow.Name,
                    Start = _workflow.Name,
                    RootId = _root?.Id ?? Guid.Empty,
                    Id = Id,
                    Status = IsWorkflowTerminatable(_workflow) ? WorkflowStatus.Done : WorkflowStatus.Running,
                    Nodes = _states.Select(kv => new WorkflowSnapshot
                    {
                        Name = kv.Key,
                        Status = kv.Value.Status,
                        Error = kv.Value.Error,
                        Payload = kv.Value.Payload?.ToJsonNode(),
                        Session = kv.Value.HasSession 
                                ? Extension.ToJsonNode(((dynamic)kv.Value).Session)
                                : null
                    }).ToArray(),
                    Forks = forks ? _states.Values
                        .Where(s => s.ForkContexts != null)
                        .SelectMany(s => s.ForkContexts!.Keys)
                        .Where(c => c._workflow != null)
                        .Select(c => c.Backup()!)
                        .ToArray() : null
                }
            :null;
    }

    /// <summary>
    /// Restore the workflow context state
    /// </summary>
    public void Restore(WorkflowContextSnapshot? snapshot)
    {
        if (snapshot is not { Status: WorkflowStatus.Running }) return;
        Id = snapshot.Id;
        
        // restore states
        foreach (var nodeSnapshot in snapshot.Nodes)
        {
            Workflow? node = _workflow?.FindByName(nodeSnapshot.Name);
            if (node == null) continue;
            
            WorkflowState state = GetOrCreateWorkflowState(nodeSnapshot.Name);
            state.Status = nodeSnapshot.Status;
            state.Error = nodeSnapshot.Error;
            state.Payload = node.PayloadType?.CreateNode(nodeSnapshot.Payload);
            if (state.HasSession && nodeSnapshot.Session != null)
            {
                Type stateType = state.GetType();
                Type sessionType = stateType.GetGenericArguments()[0];
                ((dynamic)state).Session = nodeSnapshot.Session.FromJson(sessionType);
            }
        }
        
        // restore forked contexts
        if (snapshot.Forks != null)
        {
            foreach (var forkSnapshot in snapshot.Forks)
            {
                Workflow? startNode = _workflow?.FindByName(forkSnapshot.Start);
                if (startNode == null) continue;
                
                WorkflowState state = GetOrCreateWorkflowState(startNode.Name);
                
                WorkflowContext forkContext = new WorkflowContext(_scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>(), 
                    _scheduler);
                forkContext.Initialize(Workflow, startNode, this, forkSnapshot);
                
                state.ForkContexts ??= new  ConcurrentDictionary<WorkflowContext, Guid>();
                state.ForkContexts[forkContext] = forkContext.Id; // record the forked context
            }
        }
        
        // schedule the workflow context for processing
        _scheduler.Schedule(this);
    }

    #endregion
    
    #region Methods
    
    /// <summary>
    /// Init the workflow context with app workflow node schemas
    /// </summary>
    internal void Initialize(AppWorkflowType appWorkFlow, Workflow workflow, WorkflowContext? root = null, WorkflowContextSnapshot? snapshot = null)
    {
        // init the workflow context
        Workflow = appWorkFlow;
        _workflow = workflow;
        _root = root;
        _states.Clear();

        // restore from snapshot
        if (snapshot != null)
        {
            Restore(snapshot);
            return;
        }
        
        // schedule the workflow context for processing
        // if root exists, the root will schedule this context
        if (root == null) _scheduler.Schedule(this);
        
        // save
        Persistence();
    }

    /// <summary>
    /// Gets the payload by name
    /// </summary>
    public AnySchemaNode? GetWorkflowPayload(string name)
    {
        if (!name.Contains('.'))
            return _states.TryGetValue(name, out WorkflowState? state)
                ? state.Payload
                : _root?.GetWorkflowPayload(name);
        
        // check nested payload
        string[] paths = name.Split(".", StringSplitOptions.RemoveEmptyEntries);
        AnySchemaNode? payload = GetWorkflowPayload(paths[0]);
        for (int i = 1; i < paths.Length; i++)
        {
            if (payload is not StructTypeNode @struct) return null;
            payload = @struct.GetField(paths[i]);
        }
        return payload;
    }

    /// <summary>
    /// Gets the payload by workflow
    /// </summary>
    public AnySchemaNode? GetWorkflowPayload(Workflow workflow) => GetWorkflowPayload(workflow.Name);
    
    /// <summary>
    /// Gets the workflow status by name
    /// </summary>
    public WorkflowStatus GetWorkflowStatus(string name) 
        => (_states.TryGetValue(name, out WorkflowState? state) ? state.Status : _root?.GetWorkflowStatus(name))
            ?? WorkflowStatus.Waiting;
    
    /// <summary>
    /// Gets teh workflow status by workflow
    /// </summary>
    public WorkflowStatus GetWorkflowStatus(Workflow workflow) => GetWorkflowStatus(workflow.Name);
    
    /// <summary>
    /// The workflow node is done with payload
    /// </summary>
    public void Done(string name, AnySchemaNode? payload = null)
    {
        Workflow workflow = _workflow?.FindByName(name)
            ?? throw new InvalidOperationException($"Workflow node {name} not found in the context");

        Logger.LogInformation($"[WorkflowContext]{Id} Done [Workflow] {name}");
        
        // get or create the workflow state
        WorkflowState state = GetOrCreateWorkflowState(name);
        
        // fork the workflow context for next nodes
        if (workflow.Fork && workflow != _workflow && workflow.Next is { Length: > 0 })
        {
            // Fork a new workflow context for next nodes
            WorkflowContext context = new WorkflowContext(_scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>(), 
                _scheduler);
            context.Initialize(Workflow, workflow, this);
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
        
        // save
        Persistence();
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
        
        Logger.LogError($"[WorkflowContext]{Id} Error [Workflow] {name} [Exception] {exception}");
        
        // save
        Persistence();
    }
    
    /// <summary>
    /// The workflow node has error
    /// </summary>
    public void Error(string name, Exception exception) => Error(name, exception.GetInnermostException().Message);

    /// <summary>
    /// The workflow node has error
    /// </summary>
    public void Error(Workflow workflow, string exception) => Error(workflow.Name, exception);

    /// <summary>
    /// The workflow node has error
    /// </summary>
    public void Error(Workflow workflow, Exception exception) => Error(workflow.Name, exception.GetInnermostException().Message);

    /// <summary>
    /// Goto the workflow node by name
    /// </summary>
    public void Goto(string name, string? newName = null)
    {
        Workflow _ = _workflow?.FindByName(name) ?? throw new InvalidOperationException($"Workflow node {name} not found in the context");
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
        
        // save
        Persistence();
    }

    /// <summary>
    /// Goto the workflow node
    /// </summary>
    public void Goto(Workflow workflow, string? newName = null) => Goto(workflow.Name, newName);
    
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
                await TerminateAsync();
            return;
        }
        
        Workflow workflow = next.Value.Item1;
        WorkflowState state = next.Value.Item2;
        try
        {
            Logger.LogInformation($"[WorkflowContext]{Id} Processing [Workflow] {workflow.Name}");
            
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
    public async Task TerminateAsync()
    {
        Logger.LogInformation($"[WorkflowContext]{Id} Terminated");
        
        await PersistenceAsync();
        
        foreach (var (_, value) in _states)
        {
            if (value.ForkContexts is not { Count: > 0 }) continue;
            foreach (var fork in value.ForkContexts)
            {
                await fork.Key.TerminateAsync();
            }
        }
        
        _workflow = null;
        Dispose();
    }
    
    #endregion
    
    #region Persistence

    /// <summary>
    /// Persist the workflow context state
    /// </summary>
    void Persistence(bool immediate = false)
    {
        Interlocked.Increment(ref _version);

        if (immediate)
        {
            PersistenceAsync().GetAwaiter().GetResult();
            return;
        }
        
        int curr = _version;
        Task.Run(async () =>
        {
            await Task.Delay(2000);
            if (curr != _version) return;
            await PersistenceAsync();
        });
    }
    
    /// <summary>
    /// Save the workflow context state asynchronously
    /// </summary>
    async Task PersistenceAsync()
    {
        using IServiceScope scope = _scope.ServiceProvider.CreateScope();
        IWorkflowContextPersistence? persistence = scope.ServiceProvider.GetService<IWorkflowContextPersistence>();
        if (persistence != null)
        {
            WorkflowContextSnapshot? snapshot = Backup();
            if (snapshot != null)
                await persistence.SaveAsync(snapshot);
        }
    }

    private int _version = 1;

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
        public virtual async Task ProcessAsync(WorkflowContext context, Workflow workflow)
        {
            MethodInfo processMethod = workflow.GetType().GetMethod(Components.Workflow.WORKFLOW_PROCESS_METHOD)!;
            object?[] args = [context];
            if (workflow.Args is { Length: > 0 })
            {
                args = new object?[workflow.Args.Length + 1];
                args[0] = context;
                for (int i = 0; i < workflow.Args.Length; i++)
                {
                    var arg = workflow.Args[i];
                    if (string.IsNullOrEmpty(arg.Name))
                    {
                        args[i + 1] = arg.TypeNode?.ToCSharpType().TryConvert(arg.Value);
                    }
                    else
                    {
                        AnySchemaNode? payload = context.GetWorkflowPayload(arg.Name);
                        args[i + 1] = arg.TypeNode?.ToCSharpType().TryConvert(payload);
                    }
                }
            }
            Task? task = (Task?)processMethod.Invoke(workflow, args);
            if (task != null) await task;
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
            MethodInfo processMethod = workflow.GetType().GetMethod(Components.Workflow.WORKFLOW_PROCESS_METHOD)!;
            object?[] args = [context, Session];
            if (workflow.Args is { Length: > 0 })
            {
                args = new object?[workflow.Args.Length + 2];
                args[0] = context;
                args[1] = Session;
                for (int i = 0; i < workflow.Args.Length; i++)
                {
                    var arg = workflow.Args[i];
                    if (string.IsNullOrEmpty(arg.Name))
                    {
                        args[i + 2] = arg.TypeNode?.ToCSharpType().TryConvert(arg.Value);
                    }
                    else
                    {
                        AnySchemaNode? payload = context.GetWorkflowPayload(arg.Name);
                        args[i + 2] = arg.TypeNode?.ToCSharpType().TryConvert(payload);
                    }
                }
            }
            Task<T>? task = (Task<T>?)processMethod.Invoke(workflow, args);
            if (task != null) Session = await task;
        }
        
        /// <summary>
        /// The session
        /// </summary>
        public T? Session { get; set; }
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