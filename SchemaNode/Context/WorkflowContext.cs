using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchemaNode.Components;
using SchemaNode.Components.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Utility;

namespace SchemaNode.Context;

/// <summary>
/// The workflow context
/// </summary>
public class WorkflowContext: SchemaContext
{
    #region Private Fields
    
    private WorkflowContext? _root;
    private Workflow? _workflow;
    private readonly IServiceScope _scope;
    private readonly IWorkflowScheduler _scheduler;
    private readonly ConcurrentDictionary<string, WorkflowState> _states = [];

    private const string NodeSelf = "$self";
    
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
    /// The creation time
    /// </summary>
    public DateTime CreateTime { get; private set; } = DateTime.UtcNow;

    /// <summary>
    /// The root workflow context id
    /// </summary>
    public Guid? RootId => _root?.Id;
    
    /// <summary>
    /// The workflow
    /// </summary>
    public AppWorkflowType? WorkflowType { get; private set; }
    
    /// <summary>
    /// The entry workflow node
    /// </summary>
    public Workflow? EntryWorkflow => _workflow;
    
    #endregion

    #region Backup & Restore

    /// <summary>
    /// Backup the workflow context state
    /// </summary>
    private WorkflowContextSnapshot? Backup(bool forks = false)
    {
        if (_workflow == null || WorkflowType == null) return null;
        return new WorkflowContextSnapshot
        {
            App = WorkflowType.App,
            Workflow = WorkflowType.Name,
            Start = _workflow.Name,
            CreateTime = CreateTime,
            RootId = _root?.Id ?? Guid.Empty,
            Id = Id,
            Status = IsWorkflowTerminatable(_workflow) ? WorkflowStatus.Done : WorkflowStatus.Running,
            Nodes = _states.Select(kv => new WorkflowSnapshot
            {
                Name = kv.Key,
                Status = kv.Value.Status,
                Error = kv.Value.Error,
                Payload = kv.Value.Payload?.ToJson(),
                Session = kv.Value.HasSession 
                    ? Extension.ToJsonNode((object?)((dynamic)kv.Value).Session, true)
                    : null
            }).ToArray(),
            Forks = forks
                ? _states.Values
                    .Where(s => s.ForkContexts != null)
                    .SelectMany(s => s.ForkContexts!.Values)
                    .Where(c => c._workflow != null)
                    .Select(c => c.Backup()!)
                    .ToArray()
                : null,
        };
    }

    /// <summary>
    /// Restore the workflow context state
    /// </summary>
    private void Restore(WorkflowContextSnapshot? snapshot)
    {
        if (snapshot is not { Status: WorkflowStatus.Running }) return;
        Id = snapshot.Id;
        CreateTime = snapshot.CreateTime;
        
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
                ((dynamic)state).Session = nodeSnapshot.Session.FromJson(state.GetType().GetGenericArguments()[0]);
            }
            // if the node is a fork and can't restore the session, set it to waiting
            // normally they use subscription need re-subscribe
            else if (node.Fork && state.Status == WorkflowStatus.Running) 
                state.Status = WorkflowStatus.Waiting;
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
                forkContext.Initialize(WorkflowType!, startNode, this, forkSnapshot);
                
                state.ForkContexts ??= new ConcurrentDictionary<Guid, WorkflowContext>();
                state.ForkContexts[forkContext.Id] = forkContext; // record the forked context
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
    internal void Initialize(AppWorkflowType workflowType, Workflow workflow, WorkflowContext? root = null, WorkflowContextSnapshot? snapshot = null)
    {
        // init the workflow context
        WorkflowType = workflowType;
        _workflow = workflow;
        _root = root;
        _states.Clear();
        
        // copy schema context item from root
        if (root != null) this.CopySchemaContextItem(root);

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
        return (GetWorkflowPayload(paths[0]) as StructTypeNode)?.GetValueByPaths(paths.Skip(1));
    }

    /// <summary>
    /// Gets the payload by workflow
    /// </summary>
    public AnySchemaNode? GetWorkflowPayload(Workflow workflow) => GetWorkflowPayload(workflow.Name);
    
    /// <summary>
    /// Gets the current workflow status
    /// </summary>
    /// <returns></returns>
    public WorkflowStatus GetWorkflowStatus()
        => _workflow == null
            ? WorkflowStatus.Terminated
            : GetNextWorkflowToProcess(_workflow)?.Item2.Status ?? WorkflowStatus.Terminated;
    
    /// <summary>
    /// Gets the workflow status by name
    /// </summary>
    public WorkflowStatus GetWorkflowStatus(string name) 
        => (_states.TryGetValue(name, out WorkflowState? state) ? state.Status : _root?.GetWorkflowStatus(name))
            ?? WorkflowStatus.Waiting;
    
    /// <summary>
    /// Gets the forked workflow context by id
    /// </summary>
    public WorkflowContext? GetForkedWorkflowContextById(Guid id)
    {
        if (Id == id) return this;
        
        foreach ((string name, WorkflowState value) in _states)
        {
            Workflow? workflow = _workflow?.FindByName(name);
            if (workflow == null) continue;
            if (value.ForkContexts == null) continue;
            if (value.ForkContexts.TryGetValue(id, out WorkflowContext? ctx)) return ctx;

            // full check has forks in next nodes
            if (workflow.HasForksInNextNodes)
            {
                foreach (var (_, forkContext) in value.ForkContexts)
                {
                    ctx = forkContext.GetForkedWorkflowContextById(id);
                    if (ctx != null) return ctx;
                }
            }
        }
        return null;
    }
    
    /// <summary>
    /// Gets teh workflow status by workflow
    /// </summary>
    public WorkflowStatus GetWorkflowStatus(Workflow workflow) => GetWorkflowStatus(workflow.Name);
    
    /// <summary>
    /// The workflow node is done with payload
    /// <returns>The fork workflow context if created</returns>
    /// </summary>
    public WorkflowContext? Done(string name, AnySchemaNode? payload = null, bool init = false)
    {
        Workflow workflow = _workflow?.FindByName(name)
            ?? throw new InvalidOperationException($"Workflow node {name} not found in the context");

        Logger.LogInformation($"[WorkflowContext]{Id} Done [Workflow] {name}");
        
        // get or create the workflow state
        WorkflowState state = GetOrCreateWorkflowState(name);
        
        // fork the workflow context for next nodes
        if (workflow.Fork && (workflow != _workflow || _root == null) && workflow.Next is { Length: > 0 })
        {
            // check the fork key, must all provided
            string forkKey = string.Empty;
            
            if (workflow.ForkKey is { Length: > 0 })
            {
                if (workflow.ForkKey.Length == 1 && workflow.ForkKey[0].Equals(NodeSelf))
                {
                    if (payload == null || payload.IsEmpty) return null;
                    forkKey = payload.ToString();
                }
                else
                {
                    string[] keys = new  string[workflow.ForkKey.Length];
                    for (int i = 0; i < workflow.ForkKey.Length; i++)
                    {
                        AnySchemaNode? forkKeyNode = (payload as StructTypeNode)?.GetValueByPaths(workflow.ForkKey[i]);
                        if (forkKeyNode == null) return null; // skip fork if any fork key not provided
                        keys[i] = forkKeyNode.ToString();
                    }
                    forkKey = string.Join('/', keys);
                }
            }

            // check the previous fork contexts
            if (!string.IsNullOrEmpty(forkKey) && state.ForkContexts is { IsEmpty: false })
            {
                // Check existed forks
                foreach (var (_, workflowContext) in state.ForkContexts)
                {
                    if (workflow.ForkKey!.Length == 1 && workflow.ForkKey[0].Equals(NodeSelf))
                    {
                        AnySchemaNode? forkPayload = workflowContext.GetWorkflowPayload(workflow.Name);
                        if (forkPayload == null || forkPayload.IsEmpty || !forkPayload.ToString().Equals(forkKey)) continue; 
                    }
                    else
                    {
                        var forkPayload = workflowContext.GetWorkflowPayload(workflow.Name) as StructTypeNode;
                        if (forkPayload == null) continue; // cover case but won't happen

                        string[] keys = new string[workflow.ForkKey!.Length];
                        for (int i = 0; i < workflow.ForkKey.Length; i++)
                        {
                            AnySchemaNode? forkKeyNode =
                                (payload as StructTypeNode)?.GetValueByPaths(workflow.ForkKey[i]);
                            if (forkKeyNode == null) break;
                            keys[i] = forkKeyNode.ToString();
                        }

                        if (keys.Any(string.IsNullOrEmpty) || !string.Join('/', keys).Equals(forkKey)) continue;
                    }

                    // cancel
                    if (workflow.CancelPre)
                    {
                        workflowContext.TryCancel();
                    }
                    else
                    {
                        Logger.LogDebug("[WorkflowContext]{Guid} Fork skipped for existing fork key [Workflow] {Name} [ForkKey] {Key}",
                            Id, name, forkKey);
                        return null; // skip fork
                    }
                }
            }
            
            // Fork a new workflow context for next nodes
            WorkflowContext context = new (_scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>(), _scheduler);
            context.Initialize(WorkflowType!, workflow, this);
            context.Done(workflow.Name, payload, true);

            state.ForkContexts ??= new  ConcurrentDictionary<Guid, WorkflowContext>();
            state.ForkContexts[context.Id] = context; // record the forked context
            _scheduler.Schedule(context); // schedule the new workflow context for next processing
            return context;
        }

        // record the done state
        state.Status = WorkflowStatus.Done;
        state.Payload = payload;

        // schedule the workflow context for next processing
        if (!init) _scheduler.Schedule(this); 
        
        // save
        Persistence();

        return null;
    }
    
    /// <summary>
    /// The workflow node is done with payload
    /// </summary>
    public void Done(Workflow workflow, AnySchemaNode? payload = null) => Done(workflow.Name, payload);
    
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
    /// Whether the workflow is un-cancelled
    /// </summary>
    bool IsUnCancellable(Workflow? workflow = null)
    {
        workflow ??= _workflow;
        if (workflow == null) return false;
        
        return GetOrCreateWorkflowState(workflow).Status switch
        {
            WorkflowStatus.Running => workflow.UnCancelable,
            WorkflowStatus.Waiting => false,
            _ => workflow.Next != null && workflow.Next.Any(IsUnCancellable)
        };
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
    
    void TryCancel()
    {
        if (!IsUnCancellable())
        {
            Task.Run(async () =>
            {
                await Task.Yield();
                await TerminateAsync();
            });
        }
    }

    /// <summary>
    /// Terminate the workflow context
    /// </summary>
    public async Task TerminateAsync()
    {
        Logger.LogInformation($"[WorkflowContext]{Id} Terminated");
        
        await PersistenceAsync();
        
        foreach ((string name, WorkflowState value) in _states)
        {
            // release workflow state
            Workflow? workflow = _workflow?.FindByName(name);
            if (workflow != null) await value.ReleaseAsync(this, workflow);
            
            if (value.ForkContexts is not { Count: > 0 }) continue;
            foreach ((_, WorkflowContext key) in value.ForkContexts)
                await key.TerminateAsync();
        }
        
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
        Interlocked.Increment(ref _version);
        if (_version > 10e6) Interlocked.Exchange(ref _version, 1); // reset version to avoid overflow
        
        using IServiceScope scope = _scope.ServiceProvider.CreateScope();
        var persistence = scope.ServiceProvider.GetService<IWorkflowContextPersistence>();
        if (persistence != null)
        {
            WorkflowContextSnapshot? snapshot = Backup();
            if (snapshot != null) await persistence.SaveAsync(snapshot);
        }
    }

    private int _version = 1;

    #endregion

    #region IDisposable

    public new void Dispose()
    {
        base.Dispose();

        // remove from root fork contexts
        if (_root != null && _root._states.TryGetValue(_workflow!.Name, out WorkflowState? state) 
                          && state.ForkContexts != null)
        {
            state.ForkContexts.TryRemove(Id, out _);
        }
        
        _workflow = null;
        _scope.Dispose();
    }

    #endregion

    #region Inner Type

    /// <summary>
    /// The workflow state
    /// </summary>
    private class WorkflowState
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
        public ConcurrentDictionary<Guid, WorkflowContext>? ForkContexts { get; set; }
        
        /// <summary>
        /// Has session
        /// </summary>
        public virtual bool HasSession => false;

        /// <summary>
        /// Process the workflow without session
        /// </summary>
        public virtual async Task ProcessAsync(WorkflowContext context, Workflow workflow)
        {
            MethodInfo processMethod = workflow.GetType().GetMethod(Workflow.WORKFLOW_PROCESS_METHOD)!;
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
        
        /// <summary>
        /// Release the workflow state
        /// </summary>
        public virtual Task ReleaseAsync(WorkflowContext context, Workflow workflow)
        {
            return Task.CompletedTask;
        }
    }

    /// <summary>
    ///  The workflow state with session
    /// </summary>
    private class WorkflowState<T>: WorkflowState
    {
        public override bool HasSession => true;

        /// <summary>
        /// Process the workflow with session
        /// </summary>
        public override async Task ProcessAsync(WorkflowContext context, Workflow workflow)
        {
            MethodInfo processMethod = workflow.GetType().GetMethod(Workflow.WORKFLOW_PROCESS_METHOD)!;
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
        /// Release the workflow state
        /// </summary>
        public override Task ReleaseAsync(WorkflowContext context, Workflow workflow)
        {
            return ((IWorkflowSession<T>)workflow).ReleaseSessionAsync(context, Session);
        }

        /// <summary>
        /// The session
        /// </summary>
        private T? Session { get; set; }
    }

    #endregion

    #region Utility

    /// <summary>
    /// Gets the next workflow to process
    /// </summary>
    /// <returns></returns>
    (Workflow, WorkflowState)? GetNextWorkflowToProcess(Workflow workflow)
    {
        WorkflowState state = GetOrCreateWorkflowState(workflow.Name);
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
        return _states.GetOrAdd(workflow.Name, (_) =>
        {
            Type workflowType = workflow.GetType();
            Type? inter = workflowType.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IWorkflowSession<>));
            if (inter != null)
            {
                Type sessionType = inter.GetGenericArguments()[0];
                Type stateType = typeof(WorkflowState<>).MakeGenericType(sessionType);
                return (WorkflowState)Activator.CreateInstance(stateType)!;
            }
            return new WorkflowState();
        });
    }
    
    WorkflowState GetOrCreateWorkflowState(Workflow workflow) => GetOrCreateWorkflowState(workflow.Name);
    
    #endregion
}