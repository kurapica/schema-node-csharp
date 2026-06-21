using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Utility;
using SchemaNode.Workflow;

namespace SchemaNode.Context;

/// <summary>
/// The workflow context
/// </summary>
public class WorkflowContext: SchemaContext
{
    #region Private Fields
    
    private WorkflowContext? _root;
    private BaseWorkflow? _workflow;
    private readonly IServiceScope _scope;
    private readonly IWorkflowScheduler _scheduler;
    private readonly ConcurrentDictionary<string, WorkflowState> _states = [];

    private const string NodeSelf = "$self";
    
    #endregion
    
    #region Constructors

    public WorkflowContext(IServiceScopeFactory scopeFactory, IWorkflowScheduler scheduler, ISchemaRuntime runtime): this(scopeFactory.CreateScope(), scheduler, runtime)
    {
    }
    
    private WorkflowContext(IServiceScope scope, IWorkflowScheduler scheduler, ISchemaRuntime runtime) : base(scope.ServiceProvider,  runtime)
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
    public BaseWorkflow? EntryWorkflow => _workflow;
    
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
            UpdateTime =  DateTime.UtcNow,
            RootId = _root?.Id ?? Guid.Empty,
            Id = Id,
            Status = IsWorkflowTerminatable(_workflow) ? WorkflowStatus.Done : WorkflowStatus.Running,
            Nodes = _states.Select(kv => new WorkflowNodeSnapshot
            {
                Name = kv.Key,
                Status = kv.Value.Status,
                Error = kv.Value.Error,
                Payload = kv.Value.PayloadSave ? kv.Value.Payload?.ToJson() : null,
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
            BaseWorkflow? node = _workflow?.FindByName(nodeSnapshot.Name);
            if (node == null) continue;
            
            WorkflowState state = GetOrCreateWorkflowState(nodeSnapshot.Name);
            state.Status = nodeSnapshot.Status;
            state.Error = nodeSnapshot.Error;
            state.Payload = node.PayloadType?.From(nodeSnapshot.Payload);
            if (state.HasSession && nodeSnapshot.Session != null)
            {
                try
                {
                    ((dynamic)state).Session = nodeSnapshot.Session.FromJson(state.GetType().GetGenericArguments()[0]);
                }
                catch
                {
                    // pass
                    ((dynamic)state).Session = null;
                    if (node.Fork && state.Status == WorkflowStatus.Running) 
                        state.Status = WorkflowStatus.Waiting;
                }
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
                BaseWorkflow? startNode = _workflow?.FindByName(forkSnapshot.Start);
                if (startNode == null) continue;
                
                WorkflowState state = GetOrCreateWorkflowState(startNode.Name);
                
                WorkflowContext forkContext = new WorkflowContext(_scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>(), _scheduler, Runtime);
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
    internal void Initialize(AppWorkflowType workflowType, BaseWorkflow workflow, WorkflowContext? root = null, WorkflowContextSnapshot? snapshot = null)
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
    public DataNode? GetWorkflowPayload(string name)
    {
        string[] paths = name.Split(".", 2, StringSplitOptions.RemoveEmptyEntries);
        if (paths.Length == 0) return null;
        var data = _states.TryGetValue(paths[0], out WorkflowState? state)
            ? state.Payload
            : _root?.GetWorkflowPayload(paths[0]);
        
        // check nested payload
        return paths.Length > 1 ? data?.GetAccessValue(paths[1]) : data;
    }

    /// <summary>
    /// Gets the payload by workflow
    /// </summary>
    public DataNode? GetWorkflowPayload(BaseWorkflow workflow) => GetWorkflowPayload(workflow.Name);
    
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
            BaseWorkflow? workflow = _workflow?.FindByName(name);
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
    /// Gets the forked workflow contexts by name
    /// </summary>
    public IEnumerable<WorkflowContext> GetForkedWorkflowContexts(string name)
    {
        if (!_states.TryGetValue(name, out WorkflowState? state) || state.ForkContexts == null) yield break;
        foreach ((_, WorkflowContext context) in state.ForkContexts)
            yield return context;
    }

    /// <summary>
    /// Gets the forked workflow contexts by workflow
    /// </summary>
    public IEnumerable<WorkflowContext> GetForkedWorkflowContexts(BaseWorkflow workflow)
    {
        foreach (WorkflowContext ctx in GetForkedWorkflowContexts(workflow.Name))
            yield return ctx;
    }
    
    /// <summary>
    /// Gets teh workflow status by workflow
    /// </summary>
    public WorkflowStatus GetWorkflowStatus(BaseWorkflow workflow) => GetWorkflowStatus(workflow.Name);
    
    /// <summary>
    /// The workflow node is done with payload
    /// <returns>The fork workflow context if created</returns>
    /// </summary>
    public WorkflowContext? Done(string name, DataNode? payload = null, bool init = false, Access? access = null)
    {
        BaseWorkflow workflow = _workflow?.FindByName(name)
            ?? throw new InvalidOperationException($"BaseWorkflow node {name} not found in the context");

        LogInformation($"[WorkflowContext]{Id} Done [BaseWorkflow] {name}");
        
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
                    forkKey = payload.ToString()!;
                }
                else
                {
                    string[] keys = new  string[workflow.ForkKey.Length];
                    for (int i = 0; i < workflow.ForkKey.Length; i++)
                    {
                        DataNode? forkKeyNode = (payload as StructNode)?.GetAccessValue(workflow.ForkKey[i]);
                        if (forkKeyNode == null || forkKeyNode.IsEmpty) return null; // skip fork if any fork key not provided
                        keys[i] = forkKeyNode.ToString()!;
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
                        DataNode? forkPayload = workflowContext.GetWorkflowPayload(workflow.Name);
                        if (forkPayload == null || forkPayload.IsEmpty || !forkPayload.ToString()!.Equals(forkKey)) continue; 
                    }
                    else
                    {
                        var forkPayload = workflowContext.GetWorkflowPayload(workflow.Name) as StructNode;
                        if (forkPayload == null) continue; // cover case but won't happen

                        string[] keys = new string[workflow.ForkKey!.Length];
                        for (int i = 0; i < workflow.ForkKey.Length; i++)
                        {
                            DataNode? forkKeyNode =
                                (payload as StructNode)?.GetAccessValue(workflow.ForkKey[i]);
                            if (forkKeyNode == null || forkKeyNode.IsEmpty) break;
                            keys[i] = forkKeyNode.ToString()!;
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
                        LogDebug("[WorkflowContext]{Guid} Fork skipped for existing fork key [BaseWorkflow] {Name} [ForkKey] {Key}",
                            Id, name, forkKey);
                        return null; // skip fork
                    }
                }
            }
            
            // Fork a new workflow context for next nodes
            WorkflowContext context = new (_scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>(), _scheduler, Runtime);
            context.Initialize(WorkflowType!, workflow, this);
            if (access != null) context.SetAccess(access); // switch the access
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
    public void Done(BaseWorkflow workflow, DataNode? payload = null, Access? access = null) => Done(workflow.Name, payload, false, access);
    
    /// <summary>
    /// The workflow node has error
    /// </summary>
    public void Error(string name, string exception)
    {
        WorkflowState state = GetOrCreateWorkflowState(name);
        state.Status = WorkflowStatus.Error;
        state.Error = exception;
        
        LogError($"[WorkflowContext]{Id} Error [BaseWorkflow] {name} [Exception] {exception}");
        
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
    public void Error(BaseWorkflow workflow, string exception) => Error(workflow.Name, exception);

    /// <summary>
    /// The workflow node has error
    /// </summary>
    public void Error(BaseWorkflow workflow, Exception exception) => Error(workflow.Name, exception.GetInnermostException().Message);

    /// <summary>
    /// Goto the workflow node by name
    /// </summary>
    public void Goto(string name, string? newName = null)
    {
        BaseWorkflow _ = _workflow?.FindByName(name) ?? throw new InvalidOperationException($"BaseWorkflow node {name} not found in the context");
        BaseWorkflow? newWorkflow = newName != null 
            ? _workflow?.FindByName(newName) 
                ?? throw new InvalidOperationException($"BaseWorkflow node {newName} not found in the context")
                : null;
        
        var state = GetOrCreateWorkflowState(name);
        state.Status = WorkflowStatus.Done;

        if (newWorkflow != null)
        {
            // reset the workflow states from the target node
            void ResetWorkflowState(BaseWorkflow wf)
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
    public void Goto(BaseWorkflow workflow, string? newName = null) => Goto(workflow.Name, newName);

    /// <summary>
    /// Terminate the workflow branch by name
    /// </summary>
    /// <param name="name">The workflow name</param>
    /// <param name="inner">Recursive call</param>
    /// <exception cref="InvalidOperationException"></exception>
    public void Terminate(string name, bool inner = false)
    {
        BaseWorkflow workflow = _workflow?.FindByName(name)
                                ?? throw new InvalidOperationException($"BaseWorkflow node {name} not found in the context");
        var state = GetOrCreateWorkflowState(name);
        state.Status = WorkflowStatus.Terminated;
        
        if (workflow.Next != null)
        {
            foreach (var next in workflow.Next)
            {
                Terminate(next.Name, true);
            }
        }

        // Recursion call return
        if (inner) return;
        
        LogInformation("[WorkflowContext]{Guid} Terminate Branch [BaseWorkflow] {Name}", Id, name);

        // schedule the workflow context for processing
        _scheduler.Schedule(this);
        
        // save
        Persistence();
    }
    
    /// <summary>
    /// Terminate the workflow branch
    /// </summary>
    /// <param name="workflow"></param>
    public void Terminate(BaseWorkflow workflow) => Terminate(workflow.Name);
    
    /// <summary>
    /// Whether the workflow is un-cancelled
    /// </summary>
    bool IsUnCancellable(BaseWorkflow? workflow = null)
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
        
        await _processLock.WaitAsync();
        try
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

            BaseWorkflow workflow = next.Value.Item1;
            WorkflowState state = next.Value.Item2;
            try
            {
                LogInformation($"[WorkflowContext]{Id} Processing [BaseWorkflow] {workflow.Name}");

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
        finally
        {
            _processLock.Release();
        }

        _scheduler.Schedule(this);
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
        LogInformation($"[WorkflowContext]{Id} Terminated");
        
        foreach ((string name, WorkflowState value) in _states)
        {
            // release workflow state
            BaseWorkflow? workflow = _workflow?.FindByName(name);
            if (workflow != null) await value.ReleaseAsync(this, workflow);
            if (value.Status is WorkflowStatus.Running or WorkflowStatus.Waiting)
                value.Status = WorkflowStatus.Terminated;
            
            if (value.ForkContexts is not { Count: > 0 }) continue;
            foreach ((_, WorkflowContext key) in value.ForkContexts)
                await key.TerminateAsync();
        }
        
        await PersistenceAsync();

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
        /// save payload flag
        /// </summary>
        public bool PayloadSave { get; set; }

        /// <summary>
        /// The workflow payload
        /// </summary>
        public DataNode? Payload { get; set; }
        
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
        public virtual async Task ProcessAsync(WorkflowContext context, BaseWorkflow workflow)
        {
            MethodInfo processMethod = workflow.GetType().GetMethod(BaseWorkflow.WORKFLOW_PROCESS_METHOD)!;
            ParameterInfo[] parameters = processMethod.GetParameters();
            object?[] args = [context];
            if (workflow.Args is { Length: > 0 })
            {
                args = new object?[workflow.Args.Length + 1];
                args[0] = context;
                for (int i = 0; i < workflow.Args.Length; i++)
                {
                    var arg = workflow.Args[i];
                    if (string.IsNullOrEmpty(arg.Source))
                    {
                        args[i + 1] = (arg.ValueType?.GetCsharpType() ?? parameters[i + 1].ParameterType).TryConvert(arg.Value, out object? res)  ? res : null;
                    }
                    else
                    {
                        DataNode? payload = context.GetWorkflowPayload(arg.Source);
                        args[i + 1] = payload?.GetValue(arg.ValueType?.GetCsharpType() ?? parameters[i + 1].ParameterType);
                    }
                }
            }
            Task? task = (Task?)processMethod.Invoke(workflow, args);
            if (task != null) await task;
        }
        
        /// <summary>
        /// Release the workflow state
        /// </summary>
        public virtual Task ReleaseAsync(WorkflowContext context, BaseWorkflow workflow)
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
        public override async Task ProcessAsync(WorkflowContext context, BaseWorkflow workflow)
        {
            MethodInfo processMethod = workflow.GetType().GetMethod(BaseWorkflow.WORKFLOW_PROCESS_METHOD)!;
            ParameterInfo[] parameters = processMethod.GetParameters();
            object?[] args = [context, Session];
            if (workflow.Args is { Length: > 0 })
            {
                args = new object?[workflow.Args.Length + 2];
                args[0] = context;
                args[1] = Session;
                for (int i = 0; i < workflow.Args.Length; i++)
                {
                    var arg = workflow.Args[i];
                    if (string.IsNullOrEmpty(arg.Source))
                    {
                        args[i + 2] = (arg.ValueType?.GetCsharpType() ?? parameters[i + 2].ParameterType).TryConvert(arg.Value, out object? res)  ? res : null;
                    }
                    else
                    {
                        DataNode? payload = context.GetWorkflowPayload(arg.Source);
                        args[i + 2] = payload?.GetValue(arg.ValueType?.GetCsharpType() ?? parameters[i + 2].ParameterType);
                    }
                }
            }
            Task<T>? task = (Task<T>?)processMethod.Invoke(workflow, args);
            if (task != null) Session = await task;
        }

        /// <summary>
        /// Release the workflow state
        /// </summary>
        public override async Task ReleaseAsync(WorkflowContext context, BaseWorkflow workflow)
        {
            await ((IWorkflowSession<T>)workflow).ReleaseSessionAsync(context, Session);
            Session = default;
        }

        /// <summary>
        /// The session
        /// </summary>
        public T? Session { get; internal set; }
    }

    #endregion

    #region Utility

    /// <summary>
    /// Gets the next workflow to process
    /// </summary>
    /// <returns></returns>
    (BaseWorkflow, WorkflowState)? GetNextWorkflowToProcess(BaseWorkflow workflow)
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
    bool IsWorkflowTerminatable(BaseWorkflow workflow)
    {
        if (_states.TryGetValue(workflow.Name, out WorkflowState? state))
        {
            switch (state.Status)
            {
                case WorkflowStatus.Error or WorkflowStatus.Terminated:
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
        BaseWorkflow workflow = _workflow?.FindByName(name)
            ?? throw new InvalidOperationException($"BaseWorkflow node {name} not found in the context");
        var state = _states.GetOrAdd(workflow.Name, (_) =>
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
        state.PayloadSave = workflow.PayloadSave;
        return state;
    }
    
    WorkflowState GetOrCreateWorkflowState(BaseWorkflow workflow) => GetOrCreateWorkflowState(workflow.Name);

    private readonly SemaphoreSlim _processLock = new(1,1);
    
    #endregion
}