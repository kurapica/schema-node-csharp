# 08. Workflow

Workflows are the part of the App dialect most tightly coupled to execution. A workflow is not an ordinary semantic type, but a directed acyclic graph of nodes, together with a scheduling and state-restoration mechanism.

This chapter follows the sequence "declaration → loading → execution → persistence".

---

## 1. Declaring a Workflow

### 1.1 Node Types

Workflow node types are declared in C#, and `WorkflowGenerator` then generates the `node.workflow` type. Take branch selection as an example, see [GotoWorkflow.cs](../../../SchemaNode.App/Workflow/Control/GotoWorkflow.cs#L9-L20):

```csharp
[Meta<SchemaType>($"{NS_SYSTEM_WORKFLOW_CONTROL}.goto")]
[Meta<OfNodeKind>(NODE_KIND_WORKFLOW)]
public class GotoWorkflow: BaseWorkflow
{
    public async Task ProcessAsync(WorkflowContext context, bool flag,
        [Meta<SchemaType>(typeof(NodeName))] string? trueNode,
        [Meta<SchemaType>(typeof(NodeName))] string falseNode)
    {
        await Task.Yield();
        context.Goto(this, flag ? trueNode : falseNode);
    }
}
```

Conventions:

- `[Meta<SchemaType>]` determines the node's full name in the semantic space;
- `[Meta<OfNodeKind>]` is fixed to `workflow`;
- `ProcessAsync(WorkflowContext context, ...)` is the only entry point;
- `[Meta<SchemaType>(typeof(NodeName))]` on a parameter lets the editor choose only nodes within the current workflow.

### 1.2 Node Categories

`WorkflowKind` determines how a node is classified in the editor, see [AppConstant.cs](../../../SchemaNode.App/Utility/AppConstant.cs#L49-L54):

```text
workflow     ordinary nodes (control, main flow)
call         function invocation
event        event waiting
interaction  human interaction
```

`[Meta<Forkable>(true)]` means the node can be triggered multiple times, and each trigger creates a child context. Typical examples are `WaitEvent` and `Interaction`.

### 1.3 Workflow Definition

[AppWorkflowSchema.cs](../../../SchemaNode.App/Schema/App/AppWorkflowSchema.cs#L27-L121):

```csharp
public sealed class AppWorkflowSchema: PropertyOwner
{
    public string App { get; set; }
    public string Name { get; set; }
    public bool Active { get; set; }
    public AppWorkflowNodeSchema[] Nodes { get; set; } = [];
}

public sealed class AppWorkflowNodeSchema: PropertyOwner, IErrorProvider
{
    public string Name { get; set; }
    public string Type { get; set; }
    public string Payload { get; set; }
    public CallArg[]? Args { get; set; }
    public string[]? Previous { get; set; }
    public JsonNode? State { get; set; }
    public bool? Fork { get; set; }
    public string[]? ForkKey { get; set; }
}
```

`Payload` is not filled in by hand. It is derived from the node type's `Relation`:

```csharp
[Relation<Default, Call>($"event.payload", $"{NS_SYSTEM_INTRINSIC}.assign", $"@{nameof(AppWorkflowNodeSchema.Payload)}")]
public class EventProperty : Property<EventDeclare>;
```

See [WaitEvent.cs](../../../SchemaNode.App/Workflow/Event/WaitEvent.cs#L119-L124).

Therefore the editor does not need to handle "output type" separately for each kind of node; it only follows the default relation declared by the node type.

---

## 2. Loading and Activation

Loading a workflow happens in two steps, and this is the key to understanding state restoration.

### 2.1 Step One: Parsing the Definition

`AppWorkflowType.LoadAsync` only parses properties, arguments, and payload types, see [AppWorkflowType.cs](../../../SchemaNode.App/Runtime/App/AppWorkflowType.cs#L105-L127):

```csharp
_props = _appWorkflowSchema.GetProperties(...).ToArray();
(_refTypes, Error) = await _appWorkflowSchema.LoadPropertiesAsync(context, _props);

foreach (var node in Nodes)
{
    var (_subRefTypes, err) = await node.LoadPropertiesAsync(context, node.GetProperties(...).ToArray());
    Error ??= err;
    node.PayloadValueType = await context.GetNodeTypeAsync<ValueType>(node.Payload);
}
```

This step does not create any `BaseWorkflow` instance, so it can complete while the runtime has not yet fully started.

### 2.2 Step Two: Activation

`ActivateAsync` is where the graph is actually built, see [AppWorkflowType.cs](../../../SchemaNode.App/Runtime/App/AppWorkflowType.cs#L213-L327):

```text
For each node
  ├── get the C# type of the WorkflowType
  ├── Activator.CreateInstance
  ├── inject Application / Name / Args / Fork / ForkKey / UnCancelable ...
  ├── node.LoadAsync(context, appWorkflowNodeSchema)
  └── bind Previous / Next

Validate that the entry node is unique
  ↓
Create WorkflowContext
  ↓
Try to restore the Running snapshot from IWorkflowContextPersistence
  ↓
Otherwise Initialize from the entry node
```

Two constraints follow from this:

- **Unique entry**: `topNodes.Count != 1` is treated as a definition error;
- **Immutable once activated**: saving a definition checks `IsActive`, see [SchemaStorageProviderExtension.cs](../../../SchemaNode.App/Schema/Provider/SchemaStorageProviderExtension.cs#L396-L419).

---

## 3. Execution Model

### 3.1 Node Base Class

[BaseWorkflow.cs](../../../SchemaNode.App/Workflow/BaseWorkflow.cs#L21-L128):

```csharp
public abstract class BaseWorkflow
{
    public const string WORKFLOW_PROCESS_METHOD = "ProcessAsync";

    internal AppType Application { get; set; }
    internal string Name { get; set; }
    internal BaseWorkflow[]? Previous { get; set; }
    internal BaseWorkflow[]? Next { get; set; }
    internal CallArg[]? Args { get; set; }
    internal ValueType? PayloadType { get; set; }

    internal bool Fork { get; set; }
    internal string[]? ForkKey { get; set; }
    internal bool UnCancelable { get; set; }
    internal bool CancelPre { get; set; }
    internal bool SavePayload { get; set; }

    protected void SetPayload(WorkflowContext context, object? payload = null, Access? access = null)
        => context.Done(this, payload == null ? null : PayloadType?.From(payload) ?? payload as IValueAccess, access);
}
```

`SetPayload` does two things at once:

- mark the node as done;
- write the output into the state, so that downstream nodes can read it through `GetWorkflowPayload`.

Data is not passed between nodes layer by layer through method arguments; it goes through the workflow context:

```text
Node A SetPayload → WorkflowState.Payload
Node B argument Source = "a" → context.GetWorkflowPayload("a")
```

### 3.2 Scheduling

[DefaultWorkflowScheduler.cs](../../../SchemaNode.App/Workflow/DefaultWorkflowScheduler.cs#L10-L63) uses a `Channel` plus a fixed set of worker threads:

```csharp
if (maxWorkers <= 0) maxWorkers = Math.Max(1, Environment.ProcessorCount / 4);
_channel = Channel.CreateUnbounded<WorkflowContext>(...);

_workers[i] = Task.Run(() => WorkerLoopAsync(_cts.Token));

await foreach (WorkflowContext ctx in _channel.Reader.ReadAllAsync(ct))
    await ctx.ProcessAsync();
```

- The default thread count is 1/4 of the CPU core count, reducing background usage;
- Each context is processed serially through `_processLock`, avoiding concurrency within the same context;
- `ProcessAsync` advances only one executable node at a time, then re-enqueues the context.

### 3.3 Advancement Rules

`ProcessAsync` and `GetNextWorkflowToProcess` are defined in [WorkflowContext.cs](../../../SchemaNode.App/Context/WorkflowContext.cs#L533-L575) and [the same file L817-L844](../../../SchemaNode.App/Context/WorkflowContext.cs#L817-L844):

```csharp
var next = GetNextWorkflowToProcess(_workflow);
if (next == null)
{
    if (IsWorkflowTerminatable(_workflow)) await TerminateAsync();
    return;
}

state.Status = WorkflowStatus.Running;
await state.ProcessAsync(this, workflow);
```

Selection rules:

```text
Node Waiting
  ├── some predecessor not Done → skip
  └── all predecessors Done     → execute

Node Done
  └── check Next recursively
```

So the semantics of "depends on multiple predecessors" is: **the current node executes only after all predecessors are done**. This matches the description of "dependent nodes" in tryit.

### 3.4 Argument Injection

`WorkflowState.ProcessAsync` invokes the node's `ProcessAsync` through reflection and resolves `Args` into actual arguments:

```csharp
if (string.IsNullOrEmpty(arg.Source))
    args[i + 1] = (arg.ValueType?.GetCsharpType() ?? parameters[i + 1].ParameterType)
        .TryConvert(arg.Value, out object? res) ? res : null;
else
    args[i + 1] = context.GetWorkflowPayload(arg.Source)?.GetValue(...);
```

See [WorkflowContext.cs](../../../SchemaNode.App/Context/WorkflowContext.cs#L720-L745).

There are two sources of arguments:

| Source | Description |
| --- | --- |
| Constant `Value` | A value written directly in the definition |
| Upstream `Source` | Reads a predecessor's output through `GetWorkflowPayload(name)` |

`Source` supports up to a two-segment path (`name.path`), so a field inside an upstream struct can be read.

### 3.5 Session Nodes

If a node implements `IWorkflowSession<T>`, the state class becomes `WorkflowState<T>`, and the signature of `ProcessAsync` also changes to:

```csharp
Task<T> ProcessAsync(WorkflowContext context, T session, ...);
```

The return value becomes the `Session` of the next execution, see [WorkflowContext.cs](../../../SchemaNode.App/Context/WorkflowContext.cs#L759-L807).

`WaitEvent` uses exactly this mechanism to keep event subscriptions:

```csharp
public async Task<IDisposable?> ProcessAsync(WorkflowContext context, IDisposable? session = null)
{
    session?.Dispose();
    ...
    return Fork
        ? context.SubscribeEvent<BaseEvent>(instance.GetType(), HandleEvent)
        : context.SubscribeEventOnce<BaseEvent>(instance.GetType(), HandleEvent);
}
```

See [WaitEvent.cs](../../../SchemaNode.App/Workflow/Event/WaitEvent.cs#L48-L107).

Each re-execution releases the old subscription first, avoiding duplicate subscriptions.

---

## 4. Node Types

### 4.1 Control Nodes

| Node | Behavior | Code |
| --- | --- | --- |
| `Goto` | Jump to different successors based on a condition | [GotoWorkflow.cs](../../../SchemaNode.App/Workflow/Control/GotoWorkflow.cs#L9-L20) |
| `Delay` | Complete after a delay | [DelayWorkflow.cs](../../../SchemaNode.App/Workflow/Control/DelayWorkflow.cs#L9-L23) |
| `Exit` | Terminate the whole context when the condition holds | [ExitWorkflow.cs](../../../SchemaNode.App/Workflow/Control/ExitWorkflow.cs#L9-L24) |
| `Break` | Terminate the current branch when the condition holds | [BreakWorkflow.cs](../../../SchemaNode.App/Workflow/Control/BreakWorkflow.cs#L12-L28) |
| `Access` | Switch the access context (app/target) | [AccessWorkflow.cs](../../../SchemaNode.App/Workflow/Control/AccessWorkflow.cs) |
| `TimeSchedule` | Trigger on a schedule | [TimeScheduleWorkflow.cs](../../../SchemaNode.App/Workflow/Control/TimeScheduleWorkflow.cs) |

The difference between `Break` and `Exit` is important:

```text
Break → context.Terminate(this)    terminate the current branch and its successors
Exit  → context.TerminateAsync()   terminate the whole workflow
```

### 4.2 Function Invocation

[Call.cs](../../../SchemaNode.App/Workflow/Function/Call.cs#L19-L68):

```csharp
public override async Task LoadAsync(SchemaContext context, AppWorkflowNodeSchema schema)
{
    FuncCall? call = schema.GetProperty<CallProperty>()?.Value;
    _function = await context.GetNodeTypeAsync<FunctionType>(call.Func);
    _args = call.Args;
}

public async Task ProcessAsync(WorkflowContext context)
{
    SetPayload(context, await _function.CallAsync<IValueAccess>(context,
        _args?.Select(callArg => string.IsNullOrEmpty(callArg.Source)
            ? callArg.Value?.DeepClone()
            : context.GetWorkflowPayload(callArg.Source)).ToArray() ?? [],
        PayloadType?.Name));
}
```

Functions are resolved at load time; at execution time only argument mapping and invocation happen. Exceptions are handed uniformly to `context.Error`, and the node enters the error state.

### 4.3 Waiting for Events

The key to `WaitEvent<T>` is not waiting, but **the lifetime of the subscription**:

```text
Fork = true
  → SubscribeEvent / SubscribeTopicEvent    long-lived subscription, each trigger creates a child context

Fork = false
  → SubscribeEventOnce / SubscribeTopicEventOnce  triggers only once
```

`Fork` also determines whether a child context is created on `Done`, see [WorkflowContext.cs](../../../SchemaNode.App/Context/WorkflowContext.cs#L293-L393).

### 4.4 Human Interaction

[Interaction.cs](../../../SchemaNode.App/Workflow/Interaction/Interaction.cs#L19-L24):

```csharp
public class Interaction<T>: BaseWorkflow, IWorkflowPayload<T>
{
    public Task ProcessAsync(WorkflowContext context) => Task.CompletedTask;
}
```

After execution the node stays `Running`, waiting for an external API to submit data. It relies on `Fork` to support multiple interactions, accomplished through the `WorkflowInfo` / `Interaction` APIs.

---

## 5. Fork: Child Contexts

### 5.1 Why Fork Is Needed

`WaitEvent` is a "multi-trigger" node. If all triggers shared one context, their states would overwrite each other.

So the semantics of Fork are:

```text
Each trigger
  → create a new WorkflowContext
  → use the triggering event as the payload
  → execute the subsequent nodes independently
  → destroy the child context when it finishes
```

### 5.2 ForkKey Deduplication

`ForkKey` identifies repeated triggers of "the same logical task":

```csharp
if (workflow.ForkKey is { Length: > 0 })
{
    if (workflow.ForkKey.Length == 1 && workflow.ForkKey[0].Equals(NODE_SELF))
        forkKey = payload.ToString()!;
    else
        forkKey = string.Join('/', workflow.ForkKey.Select(k => payload.GetAccessValue(k)?.ToString()));
}

if (!string.IsNullOrEmpty(forkKey) && state.ForkContexts is { IsEmpty: false })
{
    foreach (var (_, workflowContext) in state.ForkContexts)
    {
        if (same fork key)
        {
            if (workflow.CancelPre) workflowContext.TryCancel();
            else return null; // skip fork
        }
    }
}
```

See [WorkflowContext.cs](../../../SchemaNode.App/Context/WorkflowContext.cs#L304-L380).

Two options:

| Option | Behavior |
| --- | --- |
| `CancelPre = false` | Skip when a context with the same key already exists |
| `CancelPre = true` | Cancel the old context and create a new one |

`ForkKey = ["self"]` uses the payload itself as the key, suited to scenarios with "one processing chain per target".

### 5.3 Cancellation Protection

`UnCancelable` prevents cancellation of a critical branch that is executing:

```csharp
bool IsUnCancellable(BaseWorkflow? workflow = null)
{
    return GetOrCreateWorkflowState(workflow).Status switch
    {
        WorkflowStatus.Running => workflow.UnCancelable,
        WorkflowStatus.Waiting => false,
        _ => workflow.Next != null && workflow.Next.Any(IsUnCancellable)
    };
}
```

See [WorkflowContext.cs](../../../SchemaNode.App/Context/WorkflowContext.cs#L517-L528).

---

## 6. State and Persistence

### 6.1 Snapshot Structure

[WorkflowContextSnapshot.cs](../../../SchemaNode.App/Workflow/WorkflowContextSnapshot.cs):

```text
WorkflowContextSnapshot
  ├── App / Workflow / Start
  ├── Id / RootId / CreateTime / UpdateTime
  ├── Status (Running / Done)
  ├── Nodes (Status / Error / Payload / Session of each node)
  └── Forks (child context snapshots)
```

The snapshot stores only the "necessary state"; it does not store the workflow definition or function objects.

### 6.2 Backup Strategy

`Persistence` uses debouncing:

```csharp
void Persistence(bool immediate = false)
{
    Interlocked.Increment(ref _version);
    if (immediate) { PersistenceAsync().GetAwaiter().GetResult(); return; }

    int curr = _version;
    Task.Run(async () =>
    {
        await Task.Delay(2000);
        if (curr != _version) return;
        await PersistenceAsync();
    });
}
```

See [WorkflowContext.cs](../../../SchemaNode.App/Context/WorkflowContext.cs#L621-L655).

- Consecutive changes within 2 seconds write only the last one;
- `immediate` is used for scenarios that must be flushed immediately, such as termination;
- The version number prevents tasks from piling up.

### 6.3 Default Persistence Implementation

[DynamicWorkflowContextPersistence.cs](../../../SchemaNode.App/Workflow/DynamicWorkflowContextPersistence.cs#L7-L45) uses the App's own data layer to save snapshots:

```csharp
await context.BeginTransactionAsync();
await context.SaveEntityAsync(snapshot);
if (snapshot.Forks is { Length: > 0 }) await SaveForksAsync(snapshot.Forks);
await context.CommitTransactionAsync(true);
```

`CommitTransactionAsync(true)` means no data event is emitted, so that "saving the workflow state" does not itself trigger a workflow.

### 6.4 Restore

`Restore` see [WorkflowContext.cs](../../../SchemaNode.App/Context/WorkflowContext.cs#L113-L167):

```text
Restore Id / CreateTime
  ↓
Restore Status / Error / Payload / Session node by node
  ↓
Nodes with a Session and status Running → Waiting (re-subscribe)
  ↓
Restore Forks
  ↓
Reschedule
```

The key is:

```csharp
// the session (e.g. subscription) can't survive a restart, re-process the node to re-subscribe
if (state.HasSession && state.Status == WorkflowStatus.Running)
    state.Status = WorkflowStatus.Waiting;
```

Subscription objects cannot be serialized, so on restore the node is set back to `Waiting` and re-executes `ProcessAsync` to re-subscribe.

---

## 7. Workflow and Semantic Types

A workflow node type is not an independent C# executor; it belongs to the semantic space as well:

```text
WorkflowSchema (node.workflow)
  ├── Kind / Payload / Settings / Session / Args
  └── WorkflowGenerator
        ↓
RuntimeWorkflowType
        ↓
BaseWorkflow
        ↓
ProcessAsync
```

This has two consequences:

- Third parties can extend node types by adding `BaseWorkflow` subclasses, without modifying the kernel;
- The editor and the API can present and validate node types like ordinary semantic types.

---

## 8. Summary

```text
Declaration
  ├── AppWorkflowSchema + Nodes (the graph)
  └── WorkflowSchema (node types)

Loading
  ├── LoadAsync: parse properties, arguments, Payload
  └── ActivateAsync: instantiate nodes, wire the graph, validate the entry, restore the snapshot

Execution
  ├── Scheduler: Channel + Worker
  ├── ProcessAsync: advance one node at a time
  ├── GetNextWorkflowToProcess: execute only when all predecessors are Done
  ├── SetPayload / Done / Goto / Terminate
  └── Fork: child contexts + ForkKey deduplication

Persistence
  ├── Snapshot: node states + branches
  ├── debounced writes
  └── re-subscribe on restore
```

The design goal of workflows is not to "graphicalize code", but to make a flow's state, branches, waiting, and restoration all into persistable semantic structures. The next chapter discusses events—the entry point through which a workflow connects to the outside world.

> The workflow part is not yet fully open; this chapter describes the mechanisms already implemented in the code.
