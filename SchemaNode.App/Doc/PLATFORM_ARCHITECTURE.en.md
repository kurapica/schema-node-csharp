# SchemaNode.App — Platform Architecture

> SchemaNode.App is the reference application layer built on SchemaNode.Core. It provides App/AppField/AppWorkflow, Event, Workflow, Auth, dynamic tables, and the CompileContext system.

---

## 1. App Schema Family

SchemaNode.App defines a new **App Schema Family** on top of Core's Node Schema Family.

### App — Application Container

```csharp
public class AppSchema : ExtensibleSchema
{
    public string Container { get; set; }         // Parent app namespace
    public string Name { get; set; }              // App name
    public string FullName => $"{Container}.{Name}";
    public AppSchema[] Apps { get; set; }          // Sub-applications
    public AppFieldSchema[] Fields { get; set; }   // Application fields
    public AppWorkflowSchema[] Workflows { get; set; }
}
```

Apps form a **tree-structured namespace**. An App without Fields serves as a **Container** for sub-apps.

### AppField — Database Table

```csharp
public class AppFieldSchema : ExtensibleSchema
{
    public AppType App { get; set; }
    public string Name { get; set; }
    public ValueType Type { get; set; }            // Determines DB table structure
    public string? Source { get; set; }            // Input source field
    public FuncType? Push { get; set; }            // Push/conversion function
    public DataCombineType? Combine { get; set; }  // Scalar combine rule
    public DataCombine[]? Combines { get; set; }   // Struct combine rules
    public Foreign[]? Foreigns { get; set; }       // FK references
}
```

The `Type` (a ValueType) determines the database table structure. CRUD is handled by generic APIs — no per-table endpoints.

### AppWorkflow — Workflow Definition

```csharp
public class AppWorkflowSchema : ExtensibleSchema
{
    public AppWorkflowNodeSchema[] Nodes { get; set; }
}

public class AppWorkflowNodeSchema
{
    public string Name { get; set; }
    public WorkflowType Type { get; set; }
    public string[] Previous { get; set; }         // DAG incoming edges
    public string[] Next { get; set; }             // DAG outgoing edges
    public bool? Fork { get; set; }                // Stay alive for next triggers
    public string[]? ForkKey { get; set; }         // Dedup keys
    public bool? CancelPre { get; set; }           // Cancel previous forks
}
```

---

## 2. Event System

### Architecture

Topic-based pub/sub with wildcard matching, built on Reactive Extensions:

```
Topic: "myapp.orders.created"
  ├── + matches single segment: "myapp.+.created"
  └── * matches multi-segment: "myapp.*"
```

```csharp
public abstract class BaseEvent
{
    public Guid Id { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public string Topic { get; init; }
    public DataNode? Payload { get; init; }
    public bool IsTopicMatch(string pattern);  // Wildcard matching
}
```

### Event Types

**App Events** (data lifecycle):
- `AppFieldDataCreateEvent` — Data row created
- `AppFieldDataUpdateEvent` — Data row updated (payload includes `Origin`)
- `AppFieldDataDeleteEvent` — Data row deleted

**Schema Events** (schema lifecycle):
- `SchemaCreateEvent` / `SchemaDeleteEvent` / `SchemaChangeEvent`
- `AppSchemaCreateEvent` / `AppSchemaDeleteEvent` / `AppSchemaChangeEvent`

### Dispatching

```csharp
// DefaultEventDispatcher uses Reactive Extensions
public class DefaultEventDispatcher : IEventDispatcher<BaseEvent>
{
    // Global subscriptions: ConcurrentDictionary<Type, Subject<BaseEvent>>
    // Topic subscriptions: ConcurrentDictionary<Type, ConcurrentDictionary<string, Subject<BaseEvent>>>

    public async Task DispatchEvent<TE>(TE evt) where TE : BaseEvent
    {
        // Dispatch to global + topic-matched subscribers via Task.Run
    }
}
```

### Usage in SchemaContext

```csharp
// Raise an event
await context.RaiseEventAsync(new AppFieldDataCreateEvent(app, field, target, data));

// Subscribe to events
var subscription = await context.SubscribeEvent<AppFieldDataUpdateEvent>(async evt =>
{
    Console.WriteLine($"Data updated: {evt.Topic}");
});
```

---

## 3. Workflow System

### DAG-Based Orchestration

Workflows are directed acyclic graphs where each node processes and then triggers its `Next` nodes:

```
  [Start] → [WaitEvent] → [Call: process] → [Interaction: approve] → [End]
                │
                └── fork: true → processes each event independently
```

### Workflow Node Types

| Node Type | Kind | Description |
|-----------|------|-------------|
| `WaitEvent` | `event` | Subscribes to event; fork=true keeps node alive for next events |
| `Call` | `call` | Executes a schema function with arguments |
| `Interaction` | `interaction` | Waits for external API interaction |
| `Access` | control | Sets access (app + target) for downstream |
| `Break` | control | Conditional branch termination |
| `Exit` | control | Conditional full workflow termination |
| `Goto` | control | Conditional jump to named node |
| `Delay` | control | Delays execution by N milliseconds |
| `TimeSchedule` | control | Quartz.NET scheduled execution |

### Fork Semantics

When `Fork = true`:
- The node remains alive after processing
- Each trigger spawns a **child workflow** (fork)
- `ForkKey` deduplicates forks (same key = same fork)
- `CancelPre = true` cancels previous forks on new trigger

### WorkflowContext

```csharp
public class WorkflowContext : SchemaContext
{
    public Guid Id { get; }
    public WorkflowType WorkflowType { get; }
    
    public async Task ProcessAsync();                    // Main processing loop
    public void Done(BaseWorkflow wf, object? payload);  // Mark node complete
    public void Goto(BaseWorkflow wf, string nodeName);  // Conditional jump
    public Task TerminateAsync();                        // Stop all forks
}
```

### Persistence

`IWorkflowContextPersistence` enables snapshot-based persistence. `DynamicWorkflowContextPersistence` uses the same dynamic table infrastructure as App data.

---

## 4. Auth System

### Three-Level Authorization

```
Level 1: Auths (Schema / App / Workflow)
    └── Policy-based evaluator functions
    
Level 2: RowAuths (AppField row-level)
    └── Filter functions: (row: StructType) → bool
    
Level 3: ColAuths (AppField column-level)
    └── Column evaluator functions
```

### Auths — General Policies

```csharp
public class Auths : Property<PolicyItem[]>
{
    // PolicyItem: { Evaluator (function), Combine (AndAlso/OrElse), Scope }
    // PolicyScope: SchemaCreate/Read/Update/Delete, DataCreate/Read/..., 
    //              FuncExecute, WorkflowStart, WorkflowInteraction
}
```

### RowAuths — Row-Level Filtering

```csharp
public class RowAuths : Property<RowPolicy[]>
{
    // RowPolicy:
    //   Evaluator (if true, apply filter)
    //   Filter (function: StructType → bool)
}

// Example: user can only see their own data
// Evaluator: is_staff() → Filter: row.owner == current_user
// Evaluator: is_admin() → Filter: (none, sees all)
```

### ColAuths — Column-Level Filtering

```csharp
public class ColAuths : Property<ColPolicy[]>
{
    // ColPolicy:
    //   Name: struct field name
    //   Evaluators: array of evaluator functions
}
```

---

## 5. Dynamic Tables & Data Management

### How It Works

1. `AppFieldSchema.Type` (ValueType) → database table structure
2. Generic APIs handle all CRUD:
   - `QueryDynamicTableAsync(schema, type, filter, skip, take, ...)`
   - `SaveDynamicTableDataAsync(schema, data, canAdd, onlyAdd, overrides)`
   - `DeleteDynamicTableDataAsync(schema, filter)`
3. ~40 APIs total for any number of tables

### Push & Source

```
Source Field (input) ──→ Push Function ──→ Target Field (aggregated)
```

When source data changes, the push function recomputes the target:

```csharp
// AppField: "total_sales" with Source="orders.amount" and Push="sum"
// When an order is created/updated/deleted:
//   total_sales = SUM(orders.amount WHERE orders.store_id = this.store_id)
```

**DataPushCompileContext** optimizes this by:
1. Detecting third-field dependencies (other AppField lookups)
2. Extracting them into separate batch queries
3. Adjusting function arguments to include pre-fetched data
4. Tracking affected data for re-computation when third-party data changes

---

## 6. CompileContext System

### QueryFilterCompileContext

Compiles `(StructType, ...args) → bool` functions into database query filter trees.

**Process:**
1. First argument must be a `StructType` (the query type)
2. Field accesses on the argument → `QueryFieldAccessExpression`
3. DisplayOnly fields resolved through default relations
4. Final logic expression → `AppSchemaDataFilter` tree

**Output tree:**
```
AppSchemaDataFilterBinary(AndAlso,
    AppSchemaDataFilterField("active"),
    AppSchemaDataFilterBinary(GreaterThanOrEqual,
        AppSchemaDataFilterField("score"),
        AppSchemaDataFilterValue(60)))
```

This tree merges with other query filters to build complex combined queries automatically.

**Dual-Use:** The same function validates data on submission AND filters data on retrieval — one source of truth.

### DataPushCompileContext

Compiles push functions, optimizing cross-table dependencies:

```
func push_summary(orders: Order[]) -> Summary
{
    store = orders[0].store_id
    manager = system.data.app.getfield("stores", store, "manager")
    region_lead = system.data.app.getfield("employees", manager, "region_lead")
    // Without optimization: 1000 orders → 1000 getfield calls
    // With DataPushCompileContext: pre-fetched in batch, passed as extra args
}
```

---

## 7. DisplayOnly & Relations

Fields marked `DisplayOnly` are not stored — their values come from relations:

```csharp
// Struct field: manager_name (DisplayOnly)
// Default relation: lookup_manager($store_id)
// When querying, manager_name is resolved via:
//   SELECT ... FROM stores WHERE store_id = $store_id
```

Supports complex queries like:
```
a.used + b.used < c.total - 10
```
Automatically converted to query trees for database execution.

---

## 8. Extension Points

| Level | What You Can Extend | Examples |
|-------|---------------------|----------|
| **Architecture** | New event types, workflow nodes, CompileContexts, API protocols, context providers | Kafka event source, GraphQL CompileContext, gRPC protocol |
| **Engineer** | Define apps, fields, workflows, functions, auth policies | Complete business application |

---

## Summary

SchemaNode.App demonstrates the power of semantic functions: one function definition → multiple execution forms (validation, filtering, push). Combined with the App schema family, event system, workflow engine, and three-level auth, it provides a complete platform for business data management — all built on SchemaNode.Core's minimal four-pillar kernel.
