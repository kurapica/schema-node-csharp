# SchemaNode.App — 功能指南

SchemaNode.App 构建应用的实用示例。

---

## 1. 定义 App 与 Field

```csharp
[Meta<SchemaApp>("sales", "orders", "销售订单")]
public class Order
{
    [Meta<Primary>]
    public string Id { get; set; }
    public string StoreId { get; set; }
    public decimal Amount { get; set; }
}
// 自动生成：App "sales"、Field "orders"、数据库表、通用 CRUD
```

### App 容器（无 Field）

```csharp
[Meta<SchemaApp>("sales", null, "销售模块")]
public class SalesModule { }
// 子 App：sales.orders、sales.invoices
```

---

## 2. 配置权限

### Schema 级

```csharp
[Meta<Auths>(new PolicyItem[]
{
    new() { 
        Evaluator = "auth.is_admin", 
        Combine = PolicyCombine.OrElse,
        Scope = PolicyScope.SchemaUpdate | PolicyScope.SchemaDelete 
    }
})]
```

### 行级（RowAuths）

```csharp
[Meta<RowAuths>(new RowPolicy[]
{
    new() { Evaluator = "auth.is_store_staff", Filter = "auth.filter_own_store" },
    new() { Evaluator = "auth.is_admin" }  // 无过滤，看全部
})]
```

过滤函数 `auth.filter_own_store`：
```
func filter_own_store(row: Order) -> bool
{
    row.store_id == system.data.getcontext("current_store_id")
}
```

### 列级（ColAuths）

```csharp
[Meta<ColAuths>(new ColPolicy[]
{
    new() { Name = "cost_price", Evaluators = ["auth.is_manager"] }
})]
```

---

## 3. Push 函数与数据聚合

```csharp
[Meta<SchemaApp>("sales", "total_amount", "销售总额")]
[Meta<Push>("system.collection.sum")]
public class TotalSales
{
    [Meta<Primary>] public string StoreId { get; set; }
    public decimal Amount { get; set; }  // Source: orders.amount
}
// 订单变更时自动重算 total_amount
```

### DisplayOnly + Relation

```csharp
public class OrderView
{
    public string StoreId { get; set; }
    [Meta<DisplayOnly>]
    public string StoreName { get; set; }  // Relation: lookup_store($StoreId)
}
```

---

## 4. 定义事件

```csharp
// 手动触发
await context.RaiseEventAsync(new OrderApprovedEvent(app, field, target)
{
    ApprovedBy = "manager@example.com"
});

// 订阅
await context.SubscribeTopicEvent<OrderApprovedEvent>(
    "sales.orders.approved",
    async evt => await SendNotification(evt.ApprovedBy, "订单已审批"));
```

---

## 5. 定义工作流

### 订单处理工作流

```
[wait_order] → [validate_order] → [approve_order] → [notify]
     │ (fork: true — 每个事件独立处理)
```

**WaitEvent 节点：**
```json
{ "name": "wait_order", "type": "event", "fork": true,
  "args": [{ "name": "topic", "value": "sales.orders.created" }],
  "next": ["validate_order"] }
```

**Call 节点：**
```json
{ "name": "validate_order", "type": "call",
  "args": [
    { "name": "func", "value": "sales.validate_order" },
    { "name": "order", "value": "$wait_order.payload.data" }
  ]}
```

**Goto 条件跳转：**
```json
{ "name": "check_amount", "type": "call",
  "args": [{ "name": "func", "value": "system.logic.gt" },
           { "name": "left", "value": "$wait_order.payload.data.amount" },
           { "name": "right", "value": 10000 }] },
{ "name": "high_value_goto", "type": "goto",
  "args": [{ "name": "flag", "value": "$check_amount.payload" },
           { "name": "trueNode", "value": "manager_approval" },
           { "name": "falseNode", "value": "auto_approve" }] }
```

---

## 6. QueryFilterCompileContext 实战

定义策略过滤函数：

```
func can_access(row: Order, user: User) -> bool
{
    row.store_id == user.store_id 
    && row.status != "deleted"
    && row.amount < user.max_amount
}
```

**一个函数，两种用途：**
- **验证**数据提交（此订单对该用户有效？）
- **过滤**数据查询（用户能看到哪些订单？）

`QueryFilterCompileContext` 自动：
1. `row.store_id` → `QueryFieldAccessExpression("store_id")`
2. 通过 Relation 解析 `DisplayOnly` 字段
3. 逻辑表达式 → `AppSchemaDataFilter` 树
4. 与其他过滤条件合并为组合查询

---

## 7. DataPushCompileContext 优化

```
func push_store_summary(orders: Order[]) -> StoreSummary
{
    store_name = system.data.app.getfield("stores", store_id, "name")
    manager = system.data.app.getfield("stores", store_id, "manager")
    region = system.data.app.getfield("employees", manager, "region")
    return StoreSummary { total = SUM(orders.amount), ... }
}
```

**无优化**：1000 条订单 → 3000 次跨表查询。

**DataPushCompileContext 优化后：**
1. 检测 `getfield` 调用
2. 提取为第三方依赖
3. 重写函数签名：`push_store_summary(orders, stores_data, employees_data)`
4. 批量预取：2 次查询
5. 追踪依赖——员工区域变更时，关联汇总自动重算

---

## 8. 关键模式总结

| 模式 | 实现 | 收益 |
|------|------|------|
| App + Field | `[Meta<SchemaApp>]` | 零 API 开发 |
| Auth | `[Meta<RowAuths>]` / `[Meta<ColAuths>]` | 细粒度权限 |
| Push | `[Meta<Push>]` | 自动数据聚合 |
| DisplayOnly | `[Meta<DisplayOnly>]` + Relation | 跨表查询 |
| Event | `RaiseEventAsync` / `SubscribeTopicEvent` | 松耦合 |
| Workflow | DAG 节点 + Fork | 复杂编排 |
| 过滤 | 语义函数 + QueryFilterCompileContext | 一函数 = 验证 + 过滤 |
| 优化 | Push 函数 + DataPushCompileContext | 批量跨表查询 |
