# 09. Events

Events are the connection points between the App and the outside world, and between the execution bodies inside the App: data changes, definition changes, and workflow triggers are all conveyed through events.

This chapter explains how events are declared, how they are generated into Schema, how they are dispatched, and the rules for topic matching.

---

## 1. Event Base Class

[BaseEvent.cs](../../../SchemaNode.App/Event/BaseEvent.cs#L13-L71):

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

The five `SchemaIgnore` properties indicate that they are not part of the event's "definition" but runtime information:

| Property | Role |
| --- | --- |
| `Id` | Unique event identifier (UUID v7, sortable by time) |
| `Timestamp` | Occurrence time |
| `Topic` | Actual event topic, of the form `app/field/target` |
| `MatchTopic` | Topic used for subscription matching, may contain wildcards |
| `Payload` | Payload data node |

### 1.1 Topic Wildcards

`IsTopicMatch` supports three wildcards:

```text
+   matches a single segment
*   matches the remaining segments
#   matches everything
```

Rules:

```csharp
if (string.IsNullOrEmpty(Topic) || Topic == "*") return true;  // subscribe to everything
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

Note the direction: `Topic` is the actual topic declared by the event, while `topic` is the pattern passed in by the subscriber. Therefore `MatchTopic = "activity/#"` matches every event whose topic starts with `activity`.

---

## 2. Application Events

### 2.1 AppEvent

[AppEvent.cs](../../../SchemaNode.App/Event/AppEvent.cs#L17-L33):

```csharp
public abstract class AppEvent(string app, string? target = null): BaseEvent
{
    public string App => app;
    public string? Target => target;
    public override string Topic => app.ToLower().Replace(".", "_");
}
```

The root topic of an application-level event is the application name, with dots replaced by underscores.

### 2.2 AppFieldEvent

```csharp
public abstract class AppFieldEvent(string app, string field, string? target = null): AppEvent(app, target)
{
    public override string Topic => Target != null ? $"{base.Topic}/{field}/{Target}" : $"{base.Topic}/{field}";
    public override string MatchTopic => Target != null ? $"{base.Topic}/{field}/{Target}" : $"{base.Topic}/{field}/*";
}
```

Two topic shapes:

```text
with target    → app/field/target
without target → app/field/* (matches all targets when subscribing)
```

### 2.3 Data Events

[AppEvent.cs](../../../SchemaNode.App/Event/AppEvent.cs#L62-L110) provides three data events:

```csharp
public class AppFieldDataCreateEvent<T>(string app, string field, string? target = null)
    : AppFieldEvent(app, field, target), IEventPayload<AppFieldPayload<T>>;

public class AppFieldDataDeleteEvent<T>(string app, string field, string? target = null)
    : AppFieldEvent(app, field, target), IEventPayload<AppFieldPayload<T>>;

public class AppFieldDataUpdateEvent<T>(string app, string field, string? target = null)
    : AppFieldEvent(app, field, target), IEventPayload<AppFieldUpdatePayload<T>>;
```

Payload structures:

```text
AppFieldPayload<T>        App / Target / Field / Data
AppFieldUpdatePayload<T>  App / Target / Field / Data / Origin
```

The update event carries both `Data` (after the change) and `Origin` (before the change), which is the basis for derived computation and auditing.

---

## 3. Definition Change Events

[SchemaEvent.cs](../../../SchemaNode.App/Event/SchemaEvent.cs#L9-L60):

```csharp
public abstract class SchemaEvent : BaseEvent, IEventPayload<string>;

[Meta<PayloadType>(typeof(AnyType))] public class SchemaCreateEvent : SchemaEvent;
[Meta<PayloadType>(typeof(AnyType))] public class SchemaDeleteEvent : SchemaEvent;
[Meta<PayloadType>(typeof(AnyType))] public class SchemaChangeEvent : SchemaEvent;

[Meta<PayloadType>(typeof(AppType))] public class AppSchemaCreateEvent : SchemaEvent;
[Meta<PayloadType>(typeof(AppType))] public class AppSchemaDeleteEvent : SchemaEvent;
[Meta<PayloadType>(typeof(AppType))] public class AppSchemaChangeEvent : SchemaEvent;
```

There is one detail worth noting:

```csharp
public abstract class SchemaEvent : BaseEvent, IEventPayload<string>;
```

The generic parameter `string` only says that the payload is a string in C#, but semantically it is not an ordinary string:

- The payload of `SchemaCreateEvent` is a **type name**;
- The payload of `AppSchemaChangeEvent` is an **application type**.

Hence `[Meta<PayloadType>]` is needed to override the type inferred from the generic parameter. At runtime, `EventSchema.Payload` prefers this Meta:

```csharp
Type? payloadType = type.GetInterfaces()
    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventPayload<>))
    ?.GetGenericArguments()[0];

eventSchema.Payload = type.GetMetaProperty<PayloadType>()?.Value ?? typeResolver(payloadType, ...);
```

See [EventGenerator.cs](../../../SchemaNode.App/Generator/EventGenerator.cs#L161-L167).

This solves the problem that "a generic parameter cannot carry a semantic type", which is also the point called out in tryit.

---

## 4. From C# Declaration to Schema

`EventGenerator` is responsible for generating `BaseEvent` subclasses into `node.event` types, see [EventGenerator.cs](../../../SchemaNode.App/Generator/EventGenerator.cs#L22-L170).

### 4.1 Basic Constraints

```csharp
if (!type.IsAssignableTo(typeof(BaseEvent))) yield break;
```

Only event subclasses are generated.

### 4.2 Constructor Parameters → Args

The constructor parameters of an event are its semantic parameters, used to construct the topic:

```csharp
ConstructorInfo[] ctors = type.GetConstructors()
    .Where(c => c.GetCustomAttribute<SchemaIgnoreAttribute>() == null).ToArray();

if (ctors.Length > 1)
    throw new Exception($"BaseEvent type {type.FullName} has multiple constructors, which is not allowed");
```

Constraints:

- At most one public constructor;
- The name, type, default value, and nullability of each parameter become a `FuncArg`;
- `[Meta<SchemaType>]` and `[Relation]` on parameters are preserved.

The generated `Args` serve two purposes:

1. The editor displays the event parameters;
2. When a workflow waits for an event, it uses the parameters to construct a concrete event instance.

### 4.3 Generics

```csharp
Type[] genericArgs = type.GetGenericArguments();
GenericParameter[]? genericDeclare = type.GetMetaProperty<Generics>()?.Value;

if (genericArgs.Length > 0 && genericDeclare is null)
    genericDeclare = genInfos.Select(g => new GenericParameter(...)).ToArray();
```

Therefore `AppFieldDataCreateEvent<T>` generates an event type with a generic parameter, and `T` is substituted in the concrete field context.

---

## 5. Runtime Event Type

[EventType.cs](../../../SchemaNode.App/Runtime/Type/EventType.cs#L16-L120) is the runtime object of an event:

```csharp
public sealed class EventType: NodeType, IValueTypeAccess, IRelationProvider
{
    public ValueType? Payload { get; private set; }
    public FunctionNodeArgument[] Args { get; private set; } = [];
}
```

`LoadAsync` does three things:

```text
1. Get EventProperty → EventSchema
2. Resolve the types of Args (generics supported)
3. Resolve the Payload type
```

After loading, event instances are created via reflection, see [EventType.cs](../../../SchemaNode.App/Runtime/Type/EventType.cs#L219-L234):

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

The workflow's `WaitEvent` uses exactly this to turn the configured `Args` into a concrete event and then subscribe.

---

## 6. Dispatch

### 6.1 EventExtensions

[EventExtensions.cs](../../../SchemaNode.App/Event/EventExtensions.cs#L13-L90) provides the unified entry point:

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

`GetEventDispatcher` searches in this order:

```text
Look for IEventDispatcher<T> by the event's concrete type
  ↓ not registered
Search recursively by BaseType
  ↓ not found
Throw an exception
```

Results are cached per type in `EventRuntimeInfo`, see [EventExtensions.cs](../../../SchemaNode.App/Event/EventExtensions.cs#L25-L37).

Thus you can register a single dispatcher for `AppEvent` and have it cover all application event subclasses.

### 6.2 DefaultEventDispatcher

[DefaultEventDispatcher.cs](../../../SchemaNode.App/Event/DefaultEventDispatcher.cs#L11-L73):

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

The structure is:

```text
_globalEventSubjects
  Type → Subject<BaseEvent>

_topicEventSubjects
  Type → { root topic → Subject<BaseEvent> }
```

Two optimization/semantic points:

1. **Bucket by type**, so that not every subscriber receives every event;
2. **Bucket by topic root**, to avoid building an index for every full topic;
3. Generic events uniformly use `GetGenericTypeDefinition()`, so `AppFieldDataCreateEvent<A>` and `AppFieldDataCreateEvent<B>` share the same dispatcher.

### 6.3 Where Topic Matching Happens

`DispatchEvent` only buckets by "root topic" and does not perform full wildcard matching. The real matching happens in the subscription wrapper:

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

See [EventExtensions.cs](../../../SchemaNode.App/Event/EventExtensions.cs#L107-L123).

Therefore:

```text
Dispatcher  → coarse-grained routing (type + root topic)
Subscribe   → fine-grained filtering (full wildcard)
```

---

## 7. Subscribe-Once

`SubscribeEventOnce` / `SubscribeTopicEventOnce` are implemented by releasing the subscription after the first hit:

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

See [EventExtensions.cs](../../../SchemaNode.App/Event/EventExtensions.cs#L133-L189).

`WaitEvent` uses this to distinguish:

```text
Fork = false → Once  ends after a single subscription
Fork = true  → keeps subscribing, forking a child context on every trigger
```

---

## 8. Events and Transactions

Events are not raised immediately on any write. The transaction commit order is:

```text
1. Data push
2. Commit
3. Raise AppFieldDataCreateEvent / AppFieldDataUpdateEvent / AppFieldDataDeleteEvent
```

See [AppDataTransactionExtension.cs](../../../SchemaNode.App/Data/AppDataTransactionExtension.cs#L161-L300).

This guarantees:

- Workflows never see uncommitted data;
- No "ghost events" are produced when a transaction rolls back;
- Derived fields are already updated before the event is raised.

---

## 9. External Event Sources

The project ships two libraries, `SchemaNode.Kafka` and `SchemaNode.RabbitMQ`. They are not part of the App project, but follow the same declaration style:

```text
Declare a BaseEvent subclass
  ↓
EventGenerator generates node.event
  ↓
Register the corresponding IEventDispatcher<BaseEvent>
  ↓
DispatchEvent after a message is received from Kafka / RabbitMQ
```

For a workflow, it does not matter where the event comes from: `WaitEvent` only cares about the event type and topic.

---

## 10. Summary

```text
Declaration
  ├── BaseEvent subclass
  ├── IEventPayload<T> (or PayloadType override)
  └── Constructor parameters → Args

Generation
  └── EventGenerator → EventSchema → EventType

Dispatch
  ├── Event type → IEventDispatcher
  ├── Bucketing by root topic
  └── Full wildcard matching in the subscription wrapper

Trigger
  ├── After a data-change transaction commits
  ├── After a definition change is saved
  └── External message adapters
```

The design principle of the event layer is: **declaration and transport are decoupled**. An event only describes "what happened" and "what the payload is"; whether it travels in-process, through Kafka, or through RabbitMQ is decided by the dispatcher implementation.

The next chapter moves on to HTTP API and extension points, stringing definitions, data, permissions, workflows, and events together into an external service.
