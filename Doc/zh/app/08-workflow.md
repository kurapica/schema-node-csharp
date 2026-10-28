# 08. 工作流

工作流是 App 方言中与执行结合最紧的部分。它不是普通的语义类型，而是一张由节点组成的有向无环图，加上一套调度与状态恢复机制。

本章按“声明 → 加载 → 执行 → 持久化”展开。

---

## 1. 工作流的声明

### 1.1 节点类型

工作流节点类型由 C# 声明，再经 `WorkflowGenerator` 生成 `node.workflow` 类型。以分支选择为例，见 [GotoWorkflow.cs](../../../SchemaNode.App/Workflow/Control/GotoWorkflow.cs#L9-L20)：

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

约定：

- `[Meta<SchemaType>]` 决定节点在语义空间中的完整名称；
- `[Meta<OfNodeKind>]` 固定为 `workflow`；
- `ProcessAsync(WorkflowContext context, ...)` 是唯一入口；
- 参数上的 `[Meta<SchemaType>(typeof(NodeName))]` 让编辑器只能选择当前工作流中的节点。

### 1.2 节点类别

`WorkflowKind` 决定节点在编辑器中的分类，见 [AppConstant.cs](../../../SchemaNode.App/Utility/AppConstant.cs#L49-L54)：

```text
workflow     普通节点（控制、主流程）
call         函数调用
event        事件等待
interaction  人工交互
```

`[Meta<Forkable>(true)]` 表示节点可以多次触发，每次触发会创建子上下文。典型的是 `WaitEvent` 与 `Interaction`。

### 1.3 工作流定义

[AppWorkflowSchema.cs](../../../SchemaNode.App/Schema/App/AppWorkflowSchema.cs#L27-L121)：

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

`Payload` 不是手填的。它由节点类型的 `Relation` 推导：

```csharp
[Relation<Default, Call>($"event.payload", $"{NS_SYSTEM_INTRINSIC}.assign", $"@{nameof(AppWorkflowNodeSchema.Payload)}")]
public class EventProperty : Property<EventDeclare>;
```

见 [WaitEvent.cs](../../../SchemaNode.App/Workflow/Event/WaitEvent.cs#L119-L124)。

因此编辑器不需要为每种节点单独处理“输出类型”，只需遵循节点类型声明的默认关系。

---

## 2. 加载与激活

工作流的加载分两步，这是理解状态恢复的关键。

### 2.1 第一步：解析定义

`AppWorkflowType.LoadAsync` 只解析属性、参数和负载类型，见 [AppWorkflowType.cs](../../../SchemaNode.App/Runtime/App/AppWorkflowType.cs#L105-L127)：

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

这一步不创建任何 `BaseWorkflow` 实例，因此可以在运行时尚未完全启动时完成。

### 2.2 第二步：激活

`ActivateAsync` 才真正建图，见 [AppWorkflowType.cs](../../../SchemaNode.App/Runtime/App/AppWorkflowType.cs#L213-L327)：

```text
对每个节点
  ├── 取 WorkflowType 的 C# 类型
  ├── Activator.CreateInstance
  ├── 注入 Application / Name / Args / Fork / ForkKey / UnCancelable ...
  ├── node.LoadAsync(context, appWorkflowNodeSchema)
  └── 绑定 Previous / Next

校验入口节点唯一
  ↓
创建 WorkflowContext
  ↓
尝试从 IWorkflowContextPersistence 恢复 Running 快照
  ↓
否则从入口节点 Initialize
```

两个约束由此落实：

- **入口唯一**：`topNodes.Count != 1` 视为定义错误；
- **已激活不可改**：保存定义时会检查 `IsActive`，见 [SchemaStorageProviderExtension.cs](../../../SchemaNode.App/Schema/Provider/SchemaStorageProviderExtension.cs#L396-L419)。

---

## 3. 执行模型

### 3.1 节点基类

[BaseWorkflow.cs](../../../SchemaNode.App/Workflow/BaseWorkflow.cs#L21-L128)：

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

`SetPayload` 同时承担两件事：

- 标记节点完成；
- 把输出写入状态，供后续节点通过 `GetWorkflowPayload` 读取。

节点之间的数据传递不是通过方法参数逐层传递，而是通过工作流上下文：

```text
节点 A SetPayload → WorkflowState.Payload
节点 B 参数 Source = "a" → context.GetWorkflowPayload("a")
```

### 3.2 调度

[DefaultWorkflowScheduler.cs](../../../SchemaNode.App/Workflow/DefaultWorkflowScheduler.cs#L10-L63) 使用 `Channel` 加固定工作线程：

```csharp
if (maxWorkers <= 0) maxWorkers = Math.Max(1, Environment.ProcessorCount / 4);
_channel = Channel.CreateUnbounded<WorkflowContext>(...);

_workers[i] = Task.Run(() => WorkerLoopAsync(_cts.Token));

await foreach (WorkflowContext ctx in _channel.Reader.ReadAllAsync(ct))
    await ctx.ProcessAsync();
```

- 默认线程数是 CPU 核数的 1/4，降低后台占用；
- 每个上下文通过 `_processLock` 串行处理，避免同一上下文并发；
- `ProcessAsync` 每次只推进一个可执行节点，然后重新入队。

### 3.3 推进规则

`ProcessAsync` 与 `GetNextWorkflowToProcess` 见 [WorkflowContext.cs](../../../SchemaNode.App/Context/WorkflowContext.cs#L533-L575) 与 [同文件 L817-L844](../../../SchemaNode.App/Context/WorkflowContext.cs#L817-L844)：

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

选择规则：

```text
节点 Waiting
  ├── 有前驱未 Done → 跳过
  └── 前驱全 Done   → 执行

节点 Done
  └── 递归检查 Next
```

因此“依赖多个前驱”的语义是：**所有前驱完成后才会执行当前节点**。这与 tryit 中“依赖节点”的描述一致。

### 3.4 参数注入

`WorkflowState.ProcessAsync` 通过反射调用节点的 `ProcessAsync`，并把 `Args` 解析成实际参数：

```csharp
if (string.IsNullOrEmpty(arg.Source))
    args[i + 1] = (arg.ValueType?.GetCsharpType() ?? parameters[i + 1].ParameterType)
        .TryConvert(arg.Value, out object? res) ? res : null;
else
    args[i + 1] = context.GetWorkflowPayload(arg.Source)?.GetValue(...);
```

见 [WorkflowContext.cs](../../../SchemaNode.App/Context/WorkflowContext.cs#L720-L745)。

有两种参数来源：

| 来源 | 说明 |
| --- | --- |
| 常量 `Value` | 定义中直接写的值 |
| 上游 `Source` | 通过 `GetWorkflowPayload(name)` 读取前驱节点的输出 |

`Source` 支持最多两段路径（`name.path`），因此可以取上游结构体中的某个字段。

### 3.5 会话节点

如果节点实现 `IWorkflowSession<T>`，状态类会变成 `WorkflowState<T>`，`ProcessAsync` 的签名也变为：

```csharp
Task<T> ProcessAsync(WorkflowContext context, T session, ...);
```

返回值作为下一次执行的 `Session`，见 [WorkflowContext.cs](../../../SchemaNode.App/Context/WorkflowContext.cs#L759-L807)。

`WaitEvent` 正是利用这个机制保存事件订阅：

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

见 [WaitEvent.cs](../../../SchemaNode.App/Workflow/Event/WaitEvent.cs#L48-L107)。

每次重新执行时先释放旧订阅，避免重复订阅。

---

## 4. 节点类型

### 4.1 控制节点

| 节点 | 行为 | 代码 |
| --- | --- | --- |
| `Goto` | 按条件跳转到不同后继 | [GotoWorkflow.cs](../../../SchemaNode.App/Workflow/Control/GotoWorkflow.cs#L9-L20) |
| `Delay` | 延时后完成 | [DelayWorkflow.cs](../../../SchemaNode.App/Workflow/Control/DelayWorkflow.cs#L9-L23) |
| `Exit` | 条件成立时终止整个上下文 | [ExitWorkflow.cs](../../../SchemaNode.App/Workflow/Control/ExitWorkflow.cs#L9-L24) |
| `Break` | 条件成立时终止当前分支 | [BreakWorkflow.cs](../../../SchemaNode.App/Workflow/Control/BreakWorkflow.cs#L12-L28) |
| `Access` | 切换访问上下文（应用/目标） | [AccessWorkflow.cs](../../../SchemaNode.App/Workflow/Control/AccessWorkflow.cs) |
| `TimeSchedule` | 定时触发 | [TimeScheduleWorkflow.cs](../../../SchemaNode.App/Workflow/Control/TimeScheduleWorkflow.cs) |

`Break` 与 `Exit` 的区别很重要：

```text
Break → context.Terminate(this)   终止当前分支及其后继
Exit  → context.TerminateAsync()  终止整个工作流
```

### 4.2 函数调用

[Call.cs](../../../SchemaNode.App/Workflow/Function/Call.cs#L19-L68)：

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

函数在加载期解析，执行期只做参数映射和调用。异常统一交给 `context.Error`，节点进入错误状态。

### 4.3 等待事件

`WaitEvent<T>` 的关键不是等待，而是**订阅的生命周期**：

```text
Fork = true
  → SubscribeEvent / SubscribeTopicEvent    长期订阅，每次触发创建子上下文

Fork = false
  → SubscribeEventOnce / SubscribeTopicEventOnce  只触发一次
```

`Fork` 还决定是否在 `Done` 时创建子上下文，见 [WorkflowContext.cs](../../../SchemaNode.App/Context/WorkflowContext.cs#L293-L393)。

### 4.4 人工交互

[Interaction.cs](../../../SchemaNode.App/Workflow/Interaction/Interaction.cs#L19-L24)：

```csharp
public class Interaction<T>: BaseWorkflow, IWorkflowPayload<T>
{
    public Task ProcessAsync(WorkflowContext context) => Task.CompletedTask;
}
```

节点执行后保持 `Running`，等待外部 API 提交数据。它依赖 `Fork` 支持多次交互，通过 `WorkflowInfo` / `Interaction` 相关 API 完成。

---

## 5. Fork：子上下文

### 5.1 为什么需要 Fork

`WaitEvent` 是“多次触发”的节点。如果所有触发共享一个上下文，状态会互相覆盖。

因此 Fork 的语义是：

```text
每次触发
  → 创建一个新的 WorkflowContext
  → 以触发事件作为 payload
  → 独立执行后续节点
  → 结束后销毁子上下文
```

### 5.2 ForkKey 去重

`ForkKey` 用于识别“同一个逻辑任务”的重复触发：

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

见 [WorkflowContext.cs](../../../SchemaNode.App/Context/WorkflowContext.cs#L304-L380)。

两个选项：

| 选项 | 行为 |
| --- | --- |
| `CancelPre = false` | 已有同键上下文时直接跳过 |
| `CancelPre = true` | 取消旧上下文，创建新上下文 |

`ForkKey = ["self"]` 表示用 payload 本身作为键，适合“每个目标一条处理链”的场景。

### 5.3 取消保护

`UnCancelable` 防止取消正在执行的关键分支：

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

见 [WorkflowContext.cs](../../../SchemaNode.App/Context/WorkflowContext.cs#L517-L528)。

---

## 6. 状态与持久化

### 6.1 快照结构

[WorkflowContextSnapshot.cs](../../../SchemaNode.App/Workflow/WorkflowContextSnapshot.cs)：

```text
WorkflowContextSnapshot
  ├── App / Workflow / Start
  ├── Id / RootId / CreateTime / UpdateTime
  ├── Status（Running / Done）
  ├── Nodes（每个节点的 Status / Error / Payload / Session）
  └── Forks（子上下文快照）
```

快照只保存“必要状态”，不保存工作流定义或函数对象。

### 6.2 备份策略

`Persistence` 采用防抖：

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

见 [WorkflowContext.cs](../../../SchemaNode.App/Context/WorkflowContext.cs#L621-L655)。

- 2 秒内的连续变更只写最后一次；
- `immediate` 用于终止等必须立刻落盘的场景；
- 版本号防止任务堆积。

### 6.3 默认持久化实现

[DynamicWorkflowContextPersistence.cs](../../../SchemaNode.App/Workflow/DynamicWorkflowContextPersistence.cs#L7-L45) 用 App 自己的数据层保存快照：

```csharp
await context.BeginTransactionAsync();
await context.SaveEntityAsync(snapshot);
if (snapshot.Forks is { Length: > 0 }) await SaveForksAsync(snapshot.Forks);
await context.CommitTransactionAsync(true);
```

`CommitTransactionAsync(true)` 表示不发出数据事件，避免“保存工作流状态”本身又触发工作流。

### 6.4 恢复

`Restore` 见 [WorkflowContext.cs](../../../SchemaNode.App/Context/WorkflowContext.cs#L113-L167)：

```text
恢复 Id / CreateTime
  ↓
逐个节点恢复 Status / Error / Payload / Session
  ↓
有 Session 且 Running 的节点 → Waiting（重新订阅）
  ↓
恢复 Forks
  ↓
重新调度
```

关键在：

```csharp
// the session (e.g. subscription) can't survive a restart, re-process the node to re-subscribe
if (state.HasSession && state.Status == WorkflowStatus.Running)
    state.Status = WorkflowStatus.Waiting;
```

订阅对象无法序列化，因此恢复时把节点重新置为 `Waiting`，让它再次执行 `ProcessAsync` 重新订阅。

---

## 7. 工作流与语义类型的关系

工作流节点类型不是独立的 C# 执行器，它同样属于语义空间：

```text
WorkflowSchema（node.workflow）
  ├── Kind / Payload / Settings / Session / Args
  └── WorkflowGenerator
        ↓
RuntimeWorkflowType
        ↓
BaseWorkflow
        ↓
ProcessAsync
```

这带来两个结果：

- 第三方可以通过新增 `BaseWorkflow` 子类扩展节点类型，无需改内核；
- 编辑器和 API 可以把节点类型当作普通语义类型来展示和校验。

---

## 8. 小结

```text
声明
  ├── AppWorkflowSchema + Nodes（图）
  └── WorkflowSchema（节点类型）

加载
  ├── LoadAsync：解析属性、参数、Payload
  └── ActivateAsync：实例化节点、连图、入口校验、恢复快照

执行
  ├── Scheduler：Channel + Worker
  ├── ProcessAsync：每次推进一个节点
  ├── GetNextWorkflowToProcess：前驱全 Done 才执行
  ├── SetPayload / Done / Goto / Terminate
  └── Fork：子上下文 + ForkKey 去重

持久化
  ├── Snapshot：节点状态 + 分支
  ├── 防抖写入
  └── 恢复时重新订阅
```

工作流的设计目标不是“把代码图形化”，而是让流程的状态、分支、等待和恢复都成为可持久化的语义结构。下一章讨论事件——它是工作流与外部世界连接的入口。

> 目前工作流部分尚未完全开放，本章描述的是代码中已经实现的机制。
