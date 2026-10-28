# 09. 事件

事件是 App 与外部世界、以及 App 内部各执行体之间的连接点：数据变更、定义变更、工作流触发都通过事件传递。

本章说明事件如何声明、如何被生成成 Schema、如何分发，以及主题匹配的规则。

---

## 1. 事件基类

[BaseEvent.cs](../../../SchemaNode.App/Event/BaseEvent.cs#L13-L71)：

```csharp
public abstract class BaseEvent
{
    [SchemaIgnore] public Guid Id { get; } = Guid.CreateVersion7();
    [SchemaIgnore] public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

    [SchemaIgnore] public virtual string Topic => string.Empty;
    [SchemaIgnore] public virtual string MatchTopic => "#";
    [SchemaIgnore] public IValueAccess? Payload { get; set; }

    public bool IsTopicMatch(string topic) { ... }
}
```

五个 `SchemaIgnore` 属性说明它们不属于事件的“定义”，而是运行时信息：

| 属性 | 作用 |
| --- | --- |
| `Id` | 事件唯一标识（UUID v7，可按时间排序） |
| `Timestamp` | 发生时间 |
| `Topic` | 事件实际主题，形如 `app/field/target` |
| `MatchTopic` | 用于订阅匹配的主题，可含通配符 |
| `Payload` | 负载数据节点 |

### 1.1 主题通配符

`IsTopicMatch` 支持三种通配符：

```text
+   匹配单段
*   匹配剩余多段
#   匹配全部
```

规则：

```csharp
if (string.IsNullOrEmpty(Topic) || Topic == "*") return true;  // 订阅全部
string[] topicParts = Topic.Split(TOPIC_SEP, ...);
string[] matchParts = topic.Split(TOPIC_SEP, ...);
if (matchParts.Length > topicParts.Length) return false;

for (int i = 0; i < matchParts.Length; i++)
{
    if (matchParts[i] == TOPIC_WILDCARD_SINGLE) continue;
    if (matchParts[i] == TOPIC_WILDCARD_MULTI || matchParts[i] == TOPIC_WILDCARD_ALL) return true;
    if (!topicParts[i].Equals(matchParts[i], StringComparison.OrdinalIgnoreCase)) return false;
}
```

注意方向：`Topic` 是事件声明的实际主题，`topic` 是订阅方传入的模式。因此 `MatchTopic = "activity/#"` 可以匹配所有以 `activity` 开头的事件。

---

## 2. 应用事件

### 2.1 AppEvent

[AppEvent.cs](../../../SchemaNode.App/Event/AppEvent.cs#L17-L33)：

```csharp
public abstract class AppEvent(string app, string? target = null): BaseEvent
{
    public string App => app;
    public string? Target => target;
    public override string Topic => app.ToLower().Replace(".", "_");
}
```

应用级事件的根主题是应用名，点号替换为下划线。

### 2.2 AppFieldEvent

```csharp
public abstract class AppFieldEvent(string app, string field, string? target = null): AppEvent(app, target)
{
    public override string Topic => Target != null ? $"{base.Topic}/{field}/{Target}" : $"{base.Topic}/{field}";
    public override string MatchTopic => Target != null ? $"{base.Topic}/{field}/{Target}" : $"{base.Topic}/{field}/*";
}
```

两种主题形态：

```text
有 target → app/field/target
无 target → app/field/*（订阅时匹配所有目标）
```

### 2.3 数据事件

[AppEvent.cs](../../../SchemaNode.App/Event/AppEvent.cs#L62-L110) 提供了三种数据事件：

```csharp
public class AppFieldDataCreateEvent<T>(string app, string field, string? target = null)
    : AppFieldEvent(app, field, target), IEventPayload<AppFieldPayload<T>>;

public class AppFieldDataDeleteEvent<T>(string app, string field, string? target = null)
    : AppFieldEvent(app, field, target), IEventPayload<AppFieldPayload<T>>;

public class AppFieldDataUpdateEvent<T>(string app, string field, string? target = null)
    : AppFieldEvent(app, field, target), IEventPayload<AppFieldUpdatePayload<T>>;
```

负载结构：

```text
AppFieldPayload<T>        App / Target / Field / Data
AppFieldUpdatePayload<T>  App / Target / Field / Data / Origin
```

更新事件同时携带 `Data`（变更后）与 `Origin`（变更前），这是派生计算与审计的基础。

---

## 3. 定义变更事件

[SchemaEvent.cs](../../../SchemaNode.App/Event/SchemaEvent.cs#L9-L60)：

```csharp
public abstract class SchemaEvent : BaseEvent, IEventPayload<string>;

[Meta<PayloadType>(typeof(AnyType))] public class SchemaCreateEvent : SchemaEvent;
[Meta<PayloadType>(typeof(AnyType))] public class SchemaDeleteEvent : SchemaEvent;
[Meta<PayloadType>(typeof(AnyType))] public class SchemaChangeEvent : SchemaEvent;

[Meta<PayloadType>(typeof(AppType))] public class AppSchemaCreateEvent : SchemaEvent;
[Meta<PayloadType>(typeof(AppType))] public class AppSchemaDeleteEvent : SchemaEvent;
[Meta<PayloadType>(typeof(AppType))] public class AppSchemaChangeEvent : SchemaEvent;
```

这里有一个值得注意的细节：

```csharp
public abstract class SchemaEvent : BaseEvent, IEventPayload<string>;
```

泛型参数 `string` 只表示负载在 C# 中是字符串，但语义上它不是普通字符串：

- `SchemaCreateEvent` 的负载是**类型名称**；
- `AppSchemaChangeEvent` 的负载是**应用类型**。

因此需要用 `[Meta<PayloadType>]` 覆盖泛型推导出的类型。运行时的 `EventSchema.Payload` 优先取这个 Meta：

```csharp
Type? payloadType = type.GetInterfaces()
    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventPayload<>))
    ?.GetGenericArguments()[0];

eventSchema.Payload = type.GetMetaProperty<PayloadType>()?.Value ?? typeResolver(payloadType, ...);
```

见 [EventGenerator.cs](../../../SchemaNode.App/Generator/EventGenerator.cs#L161-L167)。

这解决了“泛型参数无法承载语义类型”的问题，也是 tryit 中特别提到的点。

---

## 4. 从 C# 声明到 Schema

`EventGenerator` 负责把 `BaseEvent` 子类生成为 `node.event` 类型，见 [EventGenerator.cs](../../../SchemaNode.App/Generator/EventGenerator.cs#L22-L170)。

### 4.1 基本约束

```csharp
if (!type.IsAssignableTo(typeof(BaseEvent))) yield break;
```

只有事件子类会被生成。

### 4.2 构造函数参数 → Args

事件的构造参数就是它的语义参数，用于构造主题：

```csharp
ConstructorInfo[] ctors = type.GetConstructors()
    .Where(c => c.GetCustomAttribute<SchemaIgnoreAttribute>() == null).ToArray();

if (ctors.Length > 1)
    throw new Exception($"BaseEvent type {type.FullName} has multiple constructors, which is not allowed");
```

约束：

- 最多一个公开构造函数；
- 每个参数的名称、类型、默认值、可空性都会变成 `FuncArg`；
- 参数上的 `[Meta<SchemaType>]`、`[Relation]` 会被保留。

生成出的 `Args` 有两个用途：

1. 编辑器展示事件参数；
2. 工作流等待事件时，用参数构造具体的事件实例。

### 4.3 泛型

```csharp
Type[] genericArgs = type.GetGenericArguments();
GenericParameter[]? genericDeclare = type.GetMetaProperty<Generics>()?.Value;

if (genericArgs.Length > 0 && genericDeclare is null)
    genericDeclare = genInfos.Select(g => new GenericParameter(...)).ToArray();
```

因此 `AppFieldDataCreateEvent<T>` 会生成带泛型参数的事件类型，`T` 在具体字段上下文中被替换。

---

## 5. 运行时事件类型

[EventType.cs](../../../SchemaNode.App/Runtime/Type/EventType.cs#L16-L120) 是事件的运行期对象：

```csharp
public sealed class EventType: NodeType, IValueTypeAccess, IRelationProvider
{
    public ValueType? Payload { get; private set; }
    public FunctionNodeArgument[] Args { get; private set; } = [];
}
```

`LoadAsync` 做三件事：

```text
1. 取 EventProperty → EventSchema
2. 解析 Args 的类型（支持泛型）
3. 解析 Payload 类型
```

加载后，事件实例通过反射创建，见 [EventType.cs](../../../SchemaNode.App/Runtime/Type/EventType.cs#L219-L234)：

```csharp
public async Task<BaseEvent?> GetEventInstance(SchemaContext context, object?[]? args = null)
{
    var type = GetCsharpType();
    if (type == null) return null;
    if (Args == null || Args.Length == 0) return Activator.CreateInstance(type) as BaseEvent;

    object?[] genArgs = new object?[Args.Length];
    for (int i = 0; i < Args.Length; i++)
    {
        var arg = Args[i];
        var argType = (await context.GetNodeTypeAsync<ValueType>(arg.Type))?.GetCsharpType();
        if (argType == null) return null;
        genArgs[i] = argType.TryConvert(args?.ElementAtOrDefault(i), out var result) ? result : null;
    }
    return Activator.CreateInstance(type, genArgs) as BaseEvent;
}
```

工作流的 `WaitEvent` 正是通过它把配置中的 `Args` 变成具体事件，再订阅。

---

## 6. 分发

### 6.1 EventExtensions

[EventExtensions.cs](../../../SchemaNode.App/Event/EventExtensions.cs#L13-L90) 提供统一入口：

```csharp
public void RaiseEvent<TE>(TE @event) where TE : BaseEvent
    => GetEventDispatcher(context, @event.GetType())?.DispatchEvent(@event);

public void RaiseEvent<TE, TP>(TE @event, TP payLoad)
    where TE : BaseEvent, IEventPayload<TP> where TP : notnull
{
    @event.Payload = context.GetSchemaNodeAsync(payLoad).GetAwaiter().GetResult();
    context.RaiseEvent(@event);
}
```

`GetEventDispatcher` 的查找顺序是：

```text
按事件具体类型找 IEventDispatcher<T>
  ↓ 未注册
递归按 BaseType 找
  ↓ 未找到
抛出异常
```

结果按类型缓存到 `EventRuntimeInfo`，见 [EventExtensions.cs](../../../SchemaNode.App/Event/EventExtensions.cs#L25-L37)。

因此可以为 `AppEvent` 注册一个分发器，让它覆盖所有应用事件的子类。

### 6.2 DefaultEventDispatcher

[DefaultEventDispatcher.cs](../../../SchemaNode.App/Event/DefaultEventDispatcher.cs#L11-L73)：

```csharp
public class DefaultEventDispatcher : IEventDispatcher<BaseEvent>
{
    public void DispatchEvent<TE>(TE @event) where TE : BaseEvent
    {
        string? root = !string.IsNullOrEmpty(@event.Topic) ? @event.Topic.Split(['/', '.'])[0] : null;
        Type eventType = @event.GetType().IsGenericType
            ? @event.GetType().GetGenericTypeDefinition()
            : @event.GetType();

        // topic subjects
        if (root != null && _topicEventSubjects.TryGetValue(eventType, out var topicSubjects)
                        && topicSubjects.TryGetValue(root, out var subject))
            subject.OnNext(@event);

        // global subjects
        if (_globalEventSubjects.TryGetValue(@event.GetType(), out var globalSubject))
            globalSubject.OnNext(@event);
    }
}
```

结构是：

```text
_globalEventSubjects
  Type → Subject<BaseEvent>

_topicEventSubjects
  Type → { 根主题 → Subject<BaseEvent> }
```

两个优化/语义点：

1. **按类型分桶**，避免所有订阅者收到所有事件；
2. **按主题根分桶**，避免为每个完整主题建索引；
3. 泛型事件统一用 `GetGenericTypeDefinition()`，因此 `AppFieldDataCreateEvent<A>` 与 `AppFieldDataCreateEvent<B>` 共享同一个分发器。

### 6.3 主题匹配在哪发生

`DispatchEvent` 只按“根主题”分桶，不做完整通配符匹配。真正的匹配在订阅包装里：

```csharp
public IDisposable? SubscribeTopicEvent<TE>(Type eventType, string topic, Action<TE> onEvent)
{
    return GetEventDispatcher(context, typeof(TE))?.SubscribeTopicEvent(eventType, topic, (Action<TE>)Handler);

    void Handler(TE @event)
    {
        try
        {
            if (!@event.IsTopicMatch(topic)) return;
            onEvent(@event);
        }
        catch (Exception ex)
        {
            context.LogError("SubscribeTopicEvent error: {Exception}", ex);
        }
    }
}
```

见 [EventExtensions.cs](../../../SchemaNode.App/Event/EventExtensions.cs#L107-L123)。

因此：

```text
Dispatcher  → 粗粒度路由（类型 + 根主题）
Subscribe    → 细粒度过滤（完整通配符）
```

---

## 7. 一次性订阅

`SubscribeEventOnce` / `SubscribeTopicEventOnce` 的实现方式是在第一次命中后释放订阅：

```csharp
subscription = context.SubscribeEvent<TE>(eventType, Handler);
return subscription;

void Handler(TE @event)
{
    if (subscription == null) return;
    subscription?.Dispose();
    subscription = null;
    onEvent(@event);
}
```

见 [EventExtensions.cs](../../../SchemaNode.App/Event/EventExtensions.cs#L133-L189)。

`WaitEvent` 用这个区分：

```text
Fork = false → Once  订阅一次就结束
Fork = true  → 持续订阅，每次触发 fork 子上下文
```

---

## 8. 事件与事务

事件不是在任何写入时立即发出的。事务提交的顺序是：

```text
1. 数据推送
2. Commit
3. Raise AppFieldDataCreateEvent / AppFieldDataUpdateEvent / AppFieldDataDeleteEvent
```

见 [AppDataTransactionExtension.cs](../../../SchemaNode.App/Data/AppDataTransactionExtension.cs#L161-L300)。

这保证：

- 工作流不会看到未提交的数据；
- 事务回滚时不会产生“幽灵事件”；
- 派生字段在事件发出前已经完成更新。

---

## 9. 外部事件源

项目附带 `SchemaNode.Kafka`、`SchemaNode.RabbitMQ` 两个库。它们不是 App 项目的一部分，但遵循同一套声明方式：

```text
声明 BaseEvent 子类
  ↓
EventGenerator 生成 node.event
  ↓
注册对应 IEventDispatcher<BaseEvent>
  ↓
从 Kafka / RabbitMQ 收到消息后 DispatchEvent
```

对工作流而言，事件来自哪里并不重要：`WaitEvent` 只关心事件类型和主题。

---

## 10. 小结

```text
声明
  ├── BaseEvent 子类
  ├── IEventPayload<T>（或 PayloadType 覆盖）
  └── 构造函数参数 → Args

生成
  └── EventGenerator → EventSchema → EventType

分发
  ├── 事件类型 → IEventDispatcher
  ├── 根主题分桶
  └── 完整通配符匹配在订阅包装

触发
  ├── 数据变更事务提交后
  ├── 定义变更保存后
  └── 外部消息适配器
```

事件层的设计原则是：**声明与传输解耦**。事件只描述“发生了什么”和“负载是什么”，至于通过进程内、Kafka 还是 RabbitMQ 传播，由分发器实现决定。

下一章进入 HTTP API 与扩展点，把定义、数据、权限、工作流、事件串成对外服务。
