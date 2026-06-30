# SchemaNode.App — Feature Guide

Practical examples for building applications with SchemaNode.App.

---

## 1. Defining an App with Fields

```csharp
// Define the data type
[Meta<SchemaApp>("sales", "orders", "Sales Orders")]
public class Order
{
    [Meta<Primary>]
    public string Id { get; set; }
    
    public string StoreId { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
}

// This auto-generates:
// - App "sales" with field "orders"
// - Database table for orders
// - CRUD via generic APIs
```

### App Container (no fields)

```csharp
[Meta<SchemaApp>("sales", null, "Sales Module")]
public class SalesModule { }

// Sub-apps:
// sales.orders  (container: "sales")
// sales.invoices (container: "sales")
```

---

## 2. Configuring Auth

### Schema-Level Auth

```csharp
[Meta<Auths>(new PolicyItem[]
{
    new() { 
        Evaluator = "auth.is_admin", 
        Combine = PolicyCombine.OrElse,
        Scope = PolicyScope.SchemaUpdate | PolicyScope.SchemaDelete 
    }
})]
public class Order { }
```

### Row-Level Auth

```csharp
[Meta<RowAuths>(new RowPolicy[]
{
    // Staff: filter to own store
    new() { 
        Evaluator = "auth.is_store_staff",
        Filter = "auth.filter_own_store"  // row.store_id == current_user.store_id
    },
    // Admin: no filter
    new() { 
        Evaluator = "auth.is_admin" 
    }
})]
```

The filter function `auth.filter_own_store`:
```
func filter_own_store(row: Order) -> bool
{
    row.store_id == system.data.getcontext("current_store_id")
}
```

### Column-Level Auth

```csharp
[Meta<ColAuths>(new ColPolicy[]
{
    new() { Name = "cost_price", Evaluators = ["auth.is_manager"] }
})]
```

---

## 3. Push Functions & Data Aggregation

### Simple Push

```csharp
// Order field: "total_amount" with Source and Push
[Meta<SchemaApp>("sales", "total_amount", "Total Sales")]
[Meta<Push>("system.collection.sum")]
public class TotalSales
{
    [Meta<Primary>]
    public string StoreId { get; set; }
    public decimal Amount { get; set; }  // Source: orders.amount, Push: sum
}
```

When any order is created/updated/deleted, `total_amount` auto-recomputes.

### DisplayOnly with Relations

```csharp
// Order struct with DisplayOnly field
public class OrderView
{
    public string StoreId { get; set; }
    public decimal Amount { get; set; }
    
    [Meta<DisplayOnly>]
    public string StoreName { get; set; }  // From relation: lookup_store($StoreId)
}
```

The relation automatically resolves `StoreName` from the stores table at query time.

---

## 4. Defining Events

### Raising Events

```csharp
// AppFieldDataCreateEvent is auto-raised on data creation
// Manual event:
public class OrderApprovedEvent : AppFieldEvent
{
    public string ApprovedBy { get; init; }
}

await context.RaiseEventAsync(new OrderApprovedEvent(app, field, target)
{
    ApprovedBy = "manager@example.com"
});
```

### Subscribing to Events

```csharp
// Subscribe in workflow or application code
var subscription = await context.SubscribeTopicEvent<OrderApprovedEvent>(
    "sales.orders.approved",
    async evt => {
        await SendNotification(evt.ApprovedBy, "Order approved");
    }
);

// One-shot subscription
await context.SubscribeTopicEventOnce<OrderApprovedEvent>(
    "sales.orders.*",
    async evt => Console.WriteLine($"Received: {evt.Topic}")
);
```

---

## 5. Defining Workflows

### Basic Workflow

```csharp
// Workflow: Order Processing
[Meta<SchemaApp>("sales", "order_process", "Order Processing")]
public class OrderWorkflow
{
    // Node 1: Wait for order created event
    // Node 2: Call validation function
    // Node 3: Interaction (manual approval)
    // Node 4: Call notification function
}
```

### WaitEvent Node

```json
{
  "name": "wait_order",
  "type": "event",
  "fork": true,
  "args": [
    { "name": "topic", "value": "sales.orders.created" }
  ],
  "next": ["validate_order"]
}
```

When `fork: true`, the node remains alive, spawning a child workflow for each event.

### Call Node

```json
{
  "name": "validate_order",
  "type": "call",
  "args": [
    { "name": "func", "value": "sales.validate_order" },
    { "name": "order", "value": "$wait_order.payload.data" }
  ],
  "next": ["approve_order"]
}
```

### Interaction Node

```json
{
  "name": "approve_order",
  "type": "interaction",
  "payload": "myapp.order_approval"
}
```

External API calls `POST /api/workflow/interaction` with approval data to continue.

### Control Nodes

```json
{ "name": "check_amount", "type": "call",
  "args": [
    { "name": "func", "value": "system.logic.gt" },
    { "name": "left", "value": "$wait_order.payload.data.amount" },
    { "name": "right", "value": 10000 }
  ],
  "next": ["high_value_goto"]
},
{ "name": "high_value_goto", "type": "goto",
  "args": [
    { "name": "flag", "value": "$check_amount.payload" },
    { "name": "trueNode", "value": "manager_approval" },
    { "name": "falseNode", "value": "auto_approve" }
  ]
}
```

---

## 6. QueryFilterCompileContext in Action

Define a policy filter function:

```
func can_access(row: Order, user: User) -> bool
{
    row.store_id == user.store_id 
    && row.status != "deleted"
    && row.amount < user.max_amount
}
```

This single function:
- **Validates** data on submission (is this order valid for this user?)
- **Filters** data on query (which orders can this user see?)
- Can be compiled to SQL WHERE clause, in-memory predicate, or any other target

The `QueryFilterCompileContext` automatically:
1. Replaces `row.store_id` → `QueryFieldAccessExpression("store_id")`
2. Resolves `DisplayOnly` fields through relations
3. Compiles logic expression → `AppSchemaDataFilter` tree
4. Data source merges this tree with other filters for combined queries

---

## 7. DataPushCompileContext Optimization

```
func push_store_summary(orders: Order[]) -> StoreSummary
{
    store_id = orders[0].store_id
    store_name = system.data.app.getfield("stores", store_id, "name")
    manager = system.data.app.getfield("stores", store_id, "manager")
    region = system.data.app.getfield("employees", manager, "region")
    
    return StoreSummary {
        total = SUM(orders.amount),
        store = store_name,
        region = region
    }
}
```

Without optimization: submitting 1000 orders → 3000 cross-table queries.

With `DataPushCompileContext`:
1. Detects `getfield("stores", ...)` and `getfield("employees", ...)` calls
2. Extracts them as third-field dependencies
3. Rewrites function signature: `push_store_summary(orders, stores_data, employees_data)`
4. Pre-fetches all stores and employees in 2 queries
5. Tracks dependencies — when an employee's region changes, affected summaries auto-recompute

---

## 8. Summary of Key Patterns

| Pattern | How | Benefit |
|---------|-----|---------|
| App + Field | `[Meta<SchemaApp>]` on data class | Zero API development |
| Auth | `[Meta<RowAuths>]` / `[Meta<ColAuths>]` | Fine-grained access control |
| Push | `[Meta<Push>]` on AppField | Automatic data aggregation |
| DisplayOnly | `[Meta<DisplayOnly>]` + Relation | Cross-table lookups |
| Event | `RaiseEventAsync` / `SubscribeTopicEvent` | Loose coupling |
| Workflow | DAG nodes + Fork | Complex orchestration |
| Filter | Semantic function + QueryFilterCompileContext | One function = validation + filtering |
| Optimize | Push function + DataPushCompileContext | Batched cross-table queries |
