# 01. App 方言概览

SchemaNode.Core 提供定义域、公共语义共识与语义运行时，但它本身不提供业务应用所需的执行环境。

**SchemaNode.App 是在 Core 之上构建的一套语义方言**：它把应用容器、数据字段、权限、工作流、事件、数据访问等执行能力，提升为可以被语义描述、组合和执行的原型族。

本章不展开细节，而是先给出全局地图：App 方言由哪些部分组成、它们如何装配、一次业务请求会经过哪几条链路。后续章节再沿代码逐一展开。

---

## 1. App 在分层中的位置

```text
              Definition Domain
                     ↓
            Public Semantic Consensus
                     ↓
              ┌──────┴──────┐
              ↓             ↓
          App Dialect    Other Dialects
              ↓
   ┌──────────┼──────────┬───────────┐
   ↓          ↓          ↓           ↓
Application  Field   Workflow     Event
   ↓          ↓          ↓           ↓
App Runtime  Data     Scheduler   Dispatcher
```

Core 只认识 `node`、`struct`、`array`、`function` 这些公共原型。
App 在此之上新增了自己的原型：

| SchemaKind | 常量 | 说明 |
| --- | --- | --- |
| `app` | `SCHEMA_KIND_APP` | 应用容器，可嵌套形成应用命名空间 |
| `app.field` | `SCHEMA_KIND_APP_FIELD` | 应用字段，逻辑数据对象 |
| `app.workflow` | `SCHEMA_KIND_APP_WORKFLOW` | 工作流定义 |
| `app.workflow.node` | `SCHEMA_KIND_APP_WORKFLOW_NODE` | 工作流节点 |
| `node.event` | `SCHEMA_KIND_NODE_EVENT` | 事件声明 |
| `node.workflow` | `SCHEMA_KIND_NODE_WORKFLOW` | 工作流节点类型 |

这些常量集中在 [AppConstant.cs](../../../SchemaNode.App/Utility/AppConstant.cs#L11-L45)。


---

## 2. 三个核心定义

App 方言的语义定义只有三个入口类型，其余能力都通过 Property 挂载到它们之上。

### 2.1 AppSchema：应用容器

[AppSchema.cs](../../../SchemaNode.App/Schema/App/AppSchema.cs#L24-L58) 定义了应用本身：

```csharp
[Meta<SchemaKind>(SCHEMA_KIND_APP, SCHEMA_KIND_ORDER_APP)]
[Meta<Append>(typeof(Display), typeof(Description), typeof(Relations), typeof(SystemDefined))]
public sealed class AppKind;

[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP}.schema")]
[Meta<Attach>(SCHEMA_KIND_APP)]
public sealed class AppSchema: PropertyOwner, IErrorProvider
{
    public string? Container { get; set; }
    public required string Name { get; set; }
    public string FullName => $"{Container}.{Name}".Trim('.');
}
```

关键点：

- `Container` 指向父应用（类型是 `AppType`），因此应用天然可以嵌套；
- `Name` 是应用内唯一标识，`FullName` 是 `Container.Name`；
- `Append` 声明了应用可以附着的公共属性（显示名、描述、关系、系统定义标记）；
- `Attach` 让 App 方言的属性可以挂到这个 SchemaKind 上。

`AppSchema` 还携带运行期状态：`Apps`、`Fields`、`Workflows`、`NodeSchemas`、`Provider`、`LoadState`、`Error`。这些字段不会写进定义，只用于加载和合并。

### 2.2 AppFieldSchema：逻辑数据对象

[AppFieldSchema.cs](../../../SchemaNode.App/Schema/App/AppFieldSchema.cs#L22-L61) 定义字段：

```csharp
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP_FIELD}.schema")]
[Meta<TypeProvider>(nameof(Type))]
[Meta<AppProvider>(nameof(App))]
public sealed class AppFieldSchema: PropertyOwner, IErrorProvider
{
    public string App { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }   // 指向一个语义类型，通常是 array
}
```

字段不是传统意义上的表，而是“应用执行环境中的一个可被语义描述和处理的数据实体”。它的 `Type` 一般是数组类型：数组元素提供主键和字段结构，字段再通过 `View`、`Foreigns`、`DataDerive` 等 Property 决定数据从哪里来、如何关联、如何派生。当然，字段也可以是其它数据类型，例如`struct`，用于单行数据。

### 2.3 AppWorkflowSchema：流程定义

[AppWorkflowSchema.cs](../../../SchemaNode.App/Schema/App/AppWorkflowSchema.cs#L27-L121) 定义工作流和节点：

```csharp
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP_WORKFLOW}.schema")]
public sealed class AppWorkflowSchema: PropertyOwner
{
    public string App { get; set; }
    public string Name { get; set; }
    public bool Active { get; set; }
    public AppWorkflowNodeSchema[] Nodes { get; set; } = [];
}

[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP_WORKFLOW}.node")]
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

节点之间通过 `Previous` 组成有向无环图。`Payload` 不是手填的，而是由 `Relation` 从节点类型的语义函数推导（下一章展开）。

---

## 3. 模块地图

App 项目按职责分为以下目录：

| 目录 | 职责 | 关键类型 |
| --- | --- | --- |
| `Schema/App` | 应用、字段、工作流的定义体 | `AppSchema`、`AppFieldSchema`、`AppWorkflowSchema` |
| `Schema/Type` | 事件与工作流的类型声明 | `EventSchema`、`WorkflowSchema` |
| `Schema/Provider` | 定义的读取、存储、合并 | `IAppEntryProvider`、`IAppEntryStorageProvider`、`DynamicAppEntryStorageProvider` |
| `Runtime/App` | 运行期对象与加载 | `AppType`、`AppFieldType`、`AppWorkflowType` |
| `Runtime/Compile` | 编译期上下文 | `DataDeriveCompileContext` |
| `Property/App` | App 方言属性 | `Auths`、`RowAuths`、`ColAuths`、`View`、`Foreigns`、`DataDerive`、`ScopePolicy` 等 |
| `Data` | 数据访问抽象与查询 | `IAppDataProvider`、`DynamicTableSchema`、`AppSchemaDataFilter` |
| `Data/Entity` | 用应用数据存储应用定义 | `AppEntity`、`AppFieldEntity`、`NodeEntity` |
| `Data/Sql` | SQL 方言支持 | `IAppDataSqlProvider<T>`、`ISqlProvider` |
| `Workflow` | 工作流执行 | `BaseWorkflow`、`WorkflowContext`、`DefaultWorkflowScheduler`、`Control/*` |
| `Event` | 事件与分发 | `BaseEvent`、`AppEvent`、`SchemaEvent`、`DefaultEventDispatcher` |
| `Http` | HTTP 协议与 API 基础设施 | `SchemaApi`、`ISchemaApiProtocol` |
| `Api` | 具体 API | `Api/Schema/Edit`、`Api/Schema/Info`、`Api/Application` |
| `Function` | 内置语义函数 | `SystemReflectApp`、`SystemReflectWorkflow`、`SystemReflectEvent`、`SystemAppData` |
| `Generator` | 由类型生成 Schema | `WorkflowGenerator`、`EventGenerator` |
| `Service` | 运行时装配 | `AppService`、`AppRuntimeStageHandler` |
| `Attribute`、`Enum`、`Utility` | 标注、枚举、常量与转换 | `AppScopeType`、`AppConstant` |

---

## 4. 运行时装配

App 方言的入口是 [AppService.cs](../../../SchemaNode.App/Service/AppService.cs#L55-L64)：

```csharp
public static IServiceCollection AddAppSchemaAssemblies(this IServiceCollection services, params Assembly[] assemblies)
{
    services.TryAddSingleton<ISchemaRuntime, AppSchemaRuntime>();
    services.TryAddEnumerable(ServiceDescriptor.Scoped<IRuntimeStageHandler, AppRuntimeStageHandler>());
    return services.AddSchemaAssembly<AppSchemaRuntime>().AddSchemaAssemblies(assemblies);
}
```

这段代码做了两件事：

1. 用 `AppSchemaRuntime` 替换 Core 默认的 `SchemaRuntime`。AppRuntime 额外维护应用树、字段索引、事件与工作流生成器。
2. 注册 `AppRuntimeStageHandler`，让 App 方言能够介入 Core 的运行时阶段回调。

`AppRuntimeStageHandler` 在两个阶段介入：

```csharp
// 系统类型加载完成后：扫描 [App] 标注，生成系统应用与字段
public async Task OnSystemSchemaLoaded(ISchemaContext context, IEnumerable<Assembly> assemblies)

// 用户 Schema 加载前：重置应用树，避免系统 Schema 与自定义 Schema 混淆
public Task OnSchemaLoadingAsync(ISchemaContext context)
```

见 [AppRuntimeStageHandler.cs](../../../SchemaNode.App/Service/AppRuntimeStageHandler.cs#L115-L230)。

此外，它在 `OnServiceInitialization` 中完成了一批执行层基础设施的注册：

- 关键区 `ICriticalRegionProvider`；
- Quartz 调度器（定时任务）；
- `WorkflowContext`；
- `ISchemaApiProtocol`（默认 HTTP 协议）；
- 扫描所有 `SchemaApi<,>` 子类并注册为 API；
- `IEventDispatcher<BaseEvent>`、`IWorkflowScheduler`、`IWorkflowContextPersistence`；
- `Access` 与 `AccessContextItemProvider`（请求上下文）。

见 [AppRuntimeStageHandler.cs](../../../SchemaNode.App/Service/AppRuntimeStageHandler.cs#L27-L110)。

---

## 5. 四条主要执行链

App 的代码可以按“定义 → 加载 → 访问 → 执行”四条链条理解：

```text
定义链
  AppSchema / AppFieldSchema / AppWorkflowSchema
        ↓
  IAppEntryProvider ← IAppEntryStorageProvider
        ↓
  AppSchemaRuntime（应用树、字段索引、事件、工作流）

加载链
  AppSchemaRuntime
        ↓
  AppType → AppFieldType → AppWorkflowType
        ↓
  属性、关系、权限、视图、派生信息

访问链
  SchemaContext
        ↓
  AuthorizeAsync（权限）
        ↓
  PrepareFieldDataAsync（动态表）
        ↓
  IAppDataProvider（查询 / 保存 / 删除）
        ↓
  AppSchemaDataFilter（行级过滤）

执行链
  BaseEvent → DefaultEventDispatcher
        ↓
  AppWorkflowType → WorkflowContext
        ↓
  BaseWorkflow 节点 → 函数 / 控制 / 等待事件
```

每一章会沿其中一条链展开。

---

## 6. 一个字段的完整投影

为了把上述链路串起来，可以先看一个字段从定义到执行要经过哪些对象：

```text
AppFieldSchema
  ├── App            → AppType
  ├── Type           → ArrayType → StructType（字段结构）
  ├── Display        → 编辑器显示
  ├── View           → 跨应用视图
  ├── Foreigns       → 外键关联
  ├── DataDerive     → 数据来源与计算
  ├── Auths          → 字段级权限
  ├── RowAuths       → 行级权限
  ├── ColAuths       → 列级权限
  ├── Pageable       → 分页配置
  ├── EnableStorage  → 是否落库
  └── TableName      → 物理表名
        ↓
AppFieldType
  ├── ValueType      → 值类型
  ├── DynamicTable   → 动态表结构
  ├── ViewSource     → 视图来源
  ├── PushSource     → 推送来源
  ├── BlockColumns   → 列黑名单
  └── GetSchemaAsync → 对外 Schema 投影
        ↓
IAppDataProvider
  ├── QueryDynamicTableDataAsync
  ├── SaveDynamicTableDataAsync
  ├── DeleteDynamicTableDataAsync
  └── DropDynamicTableAsync
```

这也是 App 文档后续章节的组织方式：定义、运行期、数据、权限、派生、工作流、事件、API。

---

## 7. 阅读建议

| 章节 | 关注点 |
| --- | --- |
| [02. 应用语义定义](02-schema.md) | 定义怎么写、存在哪里、如何合并 |
| [03. 运行时加载](03-runtime.md) | `AppType` / `AppFieldType` 如何加载与重载 |
| [04. 数据层](04-data.md) | 动态表、Provider、CRUD |
| [05. 查询与过滤](05-query-filter.md) | 查询树、行权限过滤、分页 |
| [06. 权限](06-auth.md) | 继承式权限、Schema / Data 权限 |
| [07. 视图与派生](07-derive.md) | 外键、视图、汇总、血缘 |
| [08. 工作流](08-workflow.md) | 节点图、上下文、调度 |
| [09. 事件](09-event.md) | 事件声明、分发、主题匹配 |
| [10. HTTP API 与扩展](10-http-api.md) | API 协议、函数命名空间、扩展点 |

> App 只是一种方言实现，不是 Core 的固定形态。阅读时可以把重点放在“执行能力如何被抽象为语义”，而不是记住每个 Property 的名字。
