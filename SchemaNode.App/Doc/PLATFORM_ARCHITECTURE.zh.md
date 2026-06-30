# SchemaNode.App — 平台架构说明

> SchemaNode.App 是构建在 SchemaNode.Core 之上的参考应用层，提供 App/AppField/AppWorkflow、Event、Workflow、Auth、动态表和 CompileContext 系统。

---

## 1. App Schema 族

### App — 应用容器

```csharp
public class AppSchema : ExtensibleSchema
{
    public string Container { get; set; }         // 父应用命名空间
    public string Name { get; set; }
    public AppSchema[] Apps { get; set; }          // 子应用
    public AppFieldSchema[] Fields { get; set; }   // 字段
    public AppWorkflowSchema[] Workflows { get; set; }
}
```

App 构成**树状命名空间**。无 Field 的 App 可作为子应用的**容器**。

### AppField — 数据库表

```csharp
public class AppFieldSchema : ExtensibleSchema
{
    public ValueType Type { get; set; }            // 决定数据库表结构
    public string? Source { get; set; }            // 输入源字段
    public FuncType? Push { get; set; }            // 推送/转换函数
    public DataCombineType? Combine { get; set; }  // 标量聚合规则
    public Foreign[]? Foreigns { get; set; }       // 外键引用
}
```

`Type`（ValueType）决定数据库表结构，CRUD 由通用 API 处理。

### AppWorkflow

```csharp
public class AppWorkflowNodeSchema
{
    public string Name { get; set; }
    public WorkflowType Type { get; set; }
    public string[] Previous { get; set; }         // DAG 入边
    public string[] Next { get; set; }             // DAG 出边
    public bool? Fork { get; set; }                // 保持存活
    public string[]? ForkKey { get; set; }         // 去重键
    public bool? CancelPre { get; set; }           // 取消前序 Fork
}
```

---

## 2. Event 系统

基于 Topic 的发布/订阅，支持通配符（`+` 单级、`*`/`#` 多级），基于 Reactive Extensions。

```csharp
public abstract class BaseEvent
{
    public Guid Id { get; init; }
    public string Topic { get; init; }
    public DataNode? Payload { get; init; }
    public bool IsTopicMatch(string pattern);
}
```

**事件类型：**
- App 事件：`AppFieldDataCreateEvent`、`AppFieldDataUpdateEvent`（含 `Origin`）、`AppFieldDataDeleteEvent`
- Schema 事件：`SchemaCreateEvent`、`SchemaDeleteEvent`、`SchemaChangeEvent`、`AppSchemaCreateEvent` 等

**使用：**
```csharp
await context.RaiseEventAsync(new AppFieldDataCreateEvent(app, field, target, data));
var sub = await context.SubscribeEvent<AppFieldDataUpdateEvent>(async evt => { ... });
```

---

## 3. Workflow 系统

### DAG 编排

工作流是有向无环图，每个节点处理后触发其 `Next` 节点：

```
[Start] → [WaitEvent] → [Call: process] → [Interaction: approve] → [End]
              │
              └── fork: true → 每个事件独立处理
```

### 节点类型

| 节点 | Kind | 描述 |
|------|------|------|
| `WaitEvent` | `event` | 订阅事件；fork=true 保持存活 |
| `Call` | `call` | 执行 Schema 函数 |
| `Interaction` | `interaction` | 等待外部 API 交互 |
| `Break` | control | 条件分支终止 |
| `Exit` | control | 条件全工作流终止 |
| `Goto` | control | 条件跳转 |
| `Delay` | control | 延迟 N 毫秒 |
| `TimeSchedule` | control | Quartz.NET 定时 |

### Fork 语义

`Fork = true` 时：节点保持存活，每次触发产生子工作流。`ForkKey` 去重，`CancelPre` 取消前序 Fork。

---

## 4. Auth 系统（三级）

```
Level 1: Auths（Schema/App/Workflow 级） — 基于求值函数的策略
Level 2: RowAuths（AppField 行级） — 过滤函数: (row) → bool
Level 3: ColAuths（AppField 列级） — 列求值函数
```

```csharp
// RowAuths 示例：用户只能看到自己的数据
// Evaluator: is_staff() → Filter: row.owner == current_user
// Evaluator: is_admin() → Filter: (none, sees all)
```

---

## 5. 动态表与数据管理

1. `AppFieldSchema.Type` → 数据库表结构
2. 通用 API（~40 个）处理所有 CRUD
3. Push & Source：源数据变更 → 推送函数 → 目标更新

### DataPushCompileContext 优化

检测第三方字段依赖，批量预取，调整函数参数。避免 1000 条数据触发 1000 次查询。

---

## 6. CompileContext 系统

### QueryFilterCompileContext

`(StructType, ...args) → bool` 函数 → `AppSchemaDataFilter` 查询树：

```
AppSchemaDataFilterBinary(AndAlso,
    AppSchemaDataFilterField("active"),
    AppSchemaDataFilterBinary(GreaterThanOrEqual,
        AppSchemaDataFilterField("score"),
        AppSchemaDataFilterValue(60)))
```

**双重用途**：同一函数验证提交数据 + 过滤查询数据 — 唯一真相。

### DataPushCompileContext

编译推送函数，提取跨表依赖批量优化：

```
func push_summary(orders: Order[]) -> Summary
{
    store = orders[0].store_id
    manager = system.data.app.getfield("stores", store, "manager")
    region_lead = system.data.app.getfield("employees", manager, "region_lead")
}
// 优化后：批量预取，作为额外参数传入
```

---

## 7. DisplayOnly & Relation

`DisplayOnly` 标记的字段不存储，值来自 Relation：

```csharp
// 结构体字段：manager_name (DisplayOnly)
// 默认 Relation：lookup_manager($store_id)
// 查询时自动解析：SELECT ... FROM stores WHERE store_id = $store_id
```

支持复杂查询如 `a.used + b.used < c.total - 10`，自动转为查询树。

---

## 8. 扩展点

| 层级 | 可扩展内容 | 示例 |
|------|-----------|------|
| **架构** | 新 Event 类型、Workflow 节点、CompileContext、API 协议、Context 提供者 | Kafka 事件源、GraphQL CompileContext、gRPC |
| **工程师** | 定义 App、Field、Workflow、Function、Auth 策略 | 完整业务应用 |

---

## 总结

SchemaNode.App 展示了语义函数的力量：一个函数定义 → 多种执行形式（验证、过滤、推送）。结合 App Schema 族、事件系统、工作流引擎和三级权限，提供了完整的业务数据管理平台 — 全部构建在 SchemaNode.Core 的最小化四大支柱内核之上。
