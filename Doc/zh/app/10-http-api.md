# 10. HTTP API 与扩展点

前面的章节分别讲定义、运行时、数据、权限、派生、工作流和事件。本章把它们串成对外的 HTTP 服务，并说明如何扩展。

---

## 1. API 的抽象

### 1.1 SchemaApi

所有 API 都继承同一个泛型基类，见 [SchemaApi.cs](../../../SchemaNode.App/Http/SchemaApi.cs#L11-L78)：

```csharp
public abstract class SchemaApi<TRequest, TResponse>
    where TRequest : SchemaApiRequest
    where TResponse : SchemaApiResponse
{
    public async Task<TResponse?> _ExecuteAsync(TRequest request, ILogger logger)
    {
        Services = request.Context!.RequestServices;
        Logger = logger;
        _criticalRegionProvider = new Lazy<ICriticalRegionProvider>(...);
        SchemaContext = Services.GetRequiredService<SchemaContext>();
        return await ExecuteAsync(request, request.Context.RequestAborted);
    }

    protected virtual Task<TResponse?> ExecuteAsync(TRequest request, CancellationToken cancellationToken)
        => Task.FromResult(default(TResponse));
}
```

约定非常明确：

- 一个 API 只处理一种请求与一种响应；
- API 通过 `SchemaContext` 访问运行时；
- 需要互斥时使用 `GetLockAsync`，它是 `ICriticalRegionProvider` 的封装。

### 1.2 请求与响应

请求基类携带通用上下文，见 [SchemaApi.cs](../../../SchemaNode.App/Http/SchemaApi.cs#L85-L113)：

```csharp
public abstract class SchemaApiRequest
{
    [JsonIgnore] public HttpContext? Context { get; set; }
    [JsonIgnore] public IFormFileCollection? Files { get; set; }
    public string? Locale { get; set; }
    public DateFormatMode? DateFormat { get; set; }
    public string? TimeZone { get; set; }
}
```

响应基类反过来携带执行信息，见 [SchemaApi.cs](../../../SchemaNode.App/Http/SchemaApi.cs#L118-L161)：

```csharp
public abstract class SchemaApiResponse
{
    [JsonIgnore] public SchemaApiFile? Output { get; set; }
    public long? ExecuteTime { get; set; }
    public string? TimeZone { get; set; }
    public string? Locale { get; set; }
}
```

`Output` 不为空时，协议层会把它转成文件下载，而不是 JSON 响应。

### 1.3 处理流程

`ISchemaApiProtocol.ProcessAsync` 是统一入口，见 [ISchemaApiProtocol.cs](../../../SchemaNode.App/Http/ISchemaApiProtocol.cs#L57-L203)：

```text
读取 body
  ├── application/json      → 原文
  └── multipart/form-data   → 表单字段拼成 JSON + 文件
        ↓
反序列化（DateFormat / TimeZone / Locale）
        ↓
DataAnnotations 校验
        ↓
执行 API
        ↓
异常映射
  ├── UnauthorizedAccessException → 403
  └── 其它                        → InternalError
        ↓
填充 ExecuteTime / TimeZone / Locale
        ↓
输出 JSON 或文件
```

请求中的时区与 locale 会被写入 `SchemaContext`，后续的日期、枚举显示、错误信息都以此为准。

---

## 2. 协议

协议只负责五件事：

```csharp
IOpenApiSchema WrapRequestSchema(...);
IOpenApiSchema WrapResponseSchema(...);
TRequest ReadRequest<TRequest>(...);
IResult GenerateResult<TResponse>(...);
IResult GenerateErrorResponse(...);
```

见 [ISchemaApiProtocol.cs](../../../SchemaNode.App/Http/ISchemaApiProtocol.cs#L26-L53)。

### 2.1 默认协议

[DefaultSchemaApiProtocol.cs](../../../SchemaNode.App/Http/DefaultSchemaApiProtocol.cs#L13-L32) 直接序列化请求与响应，不增加信封。

### 2.2 JSON-RPC 协议

[JsonRpcSchemaApiProtocol.cs](../../../SchemaNode.App/Http/JsonRpcSchemaApiProtocol.cs#L14-L100) 增加标准信封：

```json
{
  "jsonrpc": "2.0",
  "id": "uuid",
  "params": { ... }
}
```

响应：

```json
{
  "jsonrpc": "2.0",
  "id": "uuid",
  "result": { ... }
}
```

错误码映射：

| SchemaApiErrorCode | JSON-RPC Code |
| --- | --- |
| `ParseFailed` | `ParseError` |
| `InvalidParams` | `InvalidParams` |
| `InternalError` | `InternalError` |

切换协议只需：

```csharp
services.WithSchemaApiProtocol<JsonRpcSchemaApiProtocol>();
```

### 2.3 无协议 API

标注 `[NoProtocol]` 的 API 不经过当前协议，始终使用默认序列化：

```csharp
type.GetCustomAttribute<NoProtocolAttribute>() != null
```

见 [Service.cs](../../../SchemaNode.App/Http/Service.cs#L194-L203)。

这适合文件下载、健康检查等不希望被信封包裹的接口。

---

## 3. 路由与注册

### 3.1 自动注册

`AppRuntimeStageHandler.OnServiceInitialization` 扫描所有 `SchemaApi<,>` 子类：

```csharp
foreach (Type type in assemblies.SelectMany(a => a.GetTypes())
             .Where(t => t.IsSubclassOfGenericType(typeof(SchemaApi<,>)) && !t.IsAbstract))
{
    Http.Service.AddApiType(type);
    services.AddTransient(type);
}
```

见 [AppRuntimeStageHandler.cs](../../../SchemaNode.App/Service/AppRuntimeStageHandler.cs#L84-L94)。

### 3.2 URL 生成

`GetRequestUrl` 把请求类型名转成短横线路径，见 [Service.cs](../../../SchemaNode.App/Http/Service.cs#L162-L182)：

```text
BatchQueryAppDataRequest
  → batch-query-app-data
  → /schema/batch-query-app-data
```

规则：

- 去掉 `Request` 后缀；
- 按大写字母分段，转小写并用 `-` 连接；
- 统一加 `prefix`，可加 `suffix`。

### 3.3 条件注册

```csharp
if (apiType.Api.FullName!.Contains("api.schema.edit", ...))
{
    if (!hasSchemaStorage) continue;
}
else if (apiType.Api.FullName!.Contains("api.schema.application", ...))
{
    if (!hasAppDataStorage || !enableAppDataApi) continue;
}
```

见 [Service.cs](../../../SchemaNode.App/Http/Service.cs#L49-L62)。

三个开关决定了 API 的子集：

| 条件 | 影响 |
| --- | --- |
| 没有 `IAppEntryStorageProvider` | 不注册编辑类 API |
| 没有 `IAppDataProvider` | 不注册应用数据 API |
| `enableAppDataApi = false` | 应用数据 API 仅供演示，默认关闭 |

### 3.4 Swagger

`SchemaApiDocumentFilter` 负责把 API 注入 OpenAPI 文档：

```csharp
var reqSchema = context.SchemaGenerator.GenerateSchema(request, context.SchemaRepository);
var resSchema = context.SchemaGenerator.GenerateSchema(response, context.SchemaRepository);

var wrappedReq = useDefaultProtocol ? reqSchema : protocol?.WrapRequestSchema(context, reqSchema) ?? reqSchema;
...
Tags = new HashSet<OpenApiTagReference> {
    new(string.Join('.', type.FullName!.Split(".").SkipLast(1).Select(s => s.ToLower())))
}
```

见 [SchemaApiDocumentFilter.cs](../../../SchemaNode.App/Http/SchemaApiDocumentFilter.cs#L12-L49)。

分组标签来自类型的命名空间，因此 `Api.Schema.Edit.App.SaveAppSchema` 会归到 `api.schema.edit.app` 组。这就是 Swagger 中 API 按能力分组的原因。

---

## 4. API 分组

### 4.1 定义编辑

| API | 作用 |
| --- | --- |
| `SaveSchema` | 保存普通类型 |
| `DeleteSchema` | 删除普通类型 |
| `SaveEnumSubList` | 保存枚举子项 |
| `SaveAppSchema` | 保存应用（含 NodeSchemas） |
| `DeleteAppSchema` | 删除应用 |
| `SaveAppFieldSchema` | 保存字段 |
| `DeleteAppFieldSchema` | 删除字段 |
| `SwapAppFieldSchema` | 调整字段顺序 |
| `SaveAppWorkflowSchema` | 保存工作流 |
| `DeleteAppWorkflowSchema` | 删除工作流 |
| `ToggleAppWorkflowSchema` | 激活/停用工作流 |

这些 API 的共同点：

```text
权限检查 → 持久化 → 重载运行时 → 发出定义变更事件
```

以 `SaveAppSchema` 为例，见 [SaveAppSchema.cs](../../../SchemaNode.App/Api/Schema/Edit/App/SaveAppSchema.cs#L16-L48)：

```csharp
public class SaveAppSchemaRequest : SchemaApiRequest
{
    [Required] public required AppSchema Schema { get; set; }
}

protected override async Task<SaveAppSchemaResponse?> ExecuteAsync(SaveAppSchemaRequest request, CancellationToken cancellationToken)
{
    return new SaveAppSchemaResponse
    {
        Result = await SchemaContext.SaveAppSchemaAsync(request.Schema)
    };
}
```

真正的逻辑在 `SchemaStorageProviderExtension` 中（第 02 章已展开），API 只负责反序列化与调用。

### 4.2 定义查询与调用

| API | 作用 |
| --- | --- |
| `GetSchema` | 查询类型定义 |
| `GetAppSchema` | 查询应用定义（含字段、工作流） |
| `Protocol` | 返回协议元信息（版本、能力） |
| `Validate` | 校验值 |
| `ValidateSchema` | 校验候选 Schema |
| `Diagnose` | 诊断运行时状态 |
| `Authorize` | 评估权限 |
| `CallFunction` | 调用语义函数 |

`GetSchema` / `GetAppSchema` 是前端编辑器的主要数据源。它们都经过 `GetSchemaAsync` 投影，因此返回的权限标记、`BlockColumns` 与当前用户一致。

`Authorize` 让前端可以在不执行操作的情况下询问“我能不能做”，见 [Authorize.cs](../../../SchemaNode.App/Api/Schema/Info/Authorize.cs)。

`CallFunction` 是语义函数的通用入口。参数与返回值都是语义值，由运行时完成类型转换。

### 4.3 应用数据

| API | 作用 |
| --- | --- |
| `BatchQueryAppData` | 批量查询字段数据 |
| `PushAppData` | 保存 / 删除 / 推送数据 |
| `Interaction` | 提交人工交互结果 |
| `WorkflowInfo` | 查询工作流实例状态 |

`BatchQueryAppData` 是第 05 章的主链路：

```text
字段权限 → 分页 → 行权限 → 查询 → 列权限 → 枚举扫描
```

`PushAppData` 是写链路：

```text
DataCreate / DataUpdate / DataDelete 权限
  ↓
逐行行权限校验
  ↓
SaveFieldDataAsync / DeleteFieldListDataAsync
  ↓
事务提交 → 派生推送 → 数据事件
```

见 [PushAppData.cs](../../../SchemaNode.App/Api/Application/PushAppData.cs#L100-L200)。

`WorkflowInfo` 用来读取工作流上下文状态、当前节点、待处理交互，它是人工节点（`Interaction`）的配套 API。

---

## 5. 语义函数

App 的语义函数按命名空间组织：

| 命名空间 | 类 | 用途 |
| --- | --- | --- |
| `system.schema.app` | `SystemReflectApp` | 应用/字段的候选、类型、主键、聚合字段 |
| `system.data.app` | `SystemAppData` | 应用数据的读写 |
| `system.schema.workflow` | `SystemReflectWorkflow` | 节点类别、参数、状态、Payload |
| `system.schema.event` | `SystemReflectEvent` | 事件参数与负载 |

### 5.1 SystemReflectApp

[App.cs](../../../SchemaNode.App/Function/App.cs#L22-L360)：

```text
getaccessentries / getaccessvaluetype
getappentries / getappfields / getappfieldentries / getappfieldtype
getappfieldprimaries / getappforeignfields / hasfields
getcombinefields / getcombinetype / isscopepolicy
```

这些函数主要服务于编辑器：决定下拉框有哪些候选、某个字段能选哪些类型、聚合字段能按哪些字段分组。

### 5.2 SystemAppData

[Data.cs](../../../SchemaNode.App/Function/Data.cs#L33-L420)：

```csharp
public static async Task<T?> get<T>(SchemaContext context, string app, string field, params object?[] args);
public static async Task<T?> getfield<T>(...);
public static async Task<bool> save<T>(...);
public static async Task<bool> delete<T>(...);
public static async Task<ArrayNode> getdatasource(...);
```

它们不是 API 的替代品，而是给“语义函数内部访问数据”用的：

- `get` 要求完整主键，并检查访问范围；
- `getfield` 是派生计算中 `system.data.app.getfield(...)` 的实现；
- `save` / `delete` 受与 API 相同的权限约束；
- `getdatasource` 用于获取一段时间内的源数据，供推送计算使用。

### 5.3 Workflow 与 Event 反射

[Workflow.cs](../../../SchemaNode.App/Function/Workflow.cs#L14-L90)：

```text
iskind / hasargs / hasstate / isforkable / getpayloadtype / getstatetype
```

[Event.cs](../../../SchemaNode.App/Function/Event.cs#L7-L34)：

```text
hasargs / withpayload
```

这些函数让节点定义可以按“节点类型的语义”动态显示字段，而不是在编辑器里写死判断。

---

## 6. 扩展点

App 方言的扩展方式基本都可以落到“替换一个接口或新增一个声明”：

| 扩展目标 | 方式 |
| --- | --- |
| 存储后端 | 实现 `IAppDataProvider`；SQL 场景再实现 `IAppDataSqlProvider<T>` + `ISqlProvider` |
| 定义来源 | 实现 `IAppEntryProvider`；需要在线编辑则实现 `IAppEntryStorageProvider` |
| 应用组合 | 多 Provider + `AppSchema.Combine` |
| 工作流节点 | 继承 `BaseWorkflow`，声明 `[Meta<SchemaType>]` 与 `[Meta<OfNodeKind>]` |
| 事件 | 继承 `BaseEvent`，实现 `IEventPayload<T>`，可选 `[Meta<PayloadType>]` |
| 事件传输 | 实现 `IEventDispatcher<BaseEvent>`（如 Kafka / RabbitMQ） |
| API | 继承 `SchemaApi<TRequest, TResponse>` |
| API 协议 | 实现 `ISchemaApiProtocol`，用 `WithSchemaApiProtocol<T>` 切换 |
| 序列化格式 | 实现 `ISchemaFormatProvider`（默认 `JsonSchemaFormatProvider`） |
| 关键区 | 实现 `ICriticalRegionProvider`（默认进程内实现） |
| 工作流持久化 | 实现 `IWorkflowContextPersistence` |
| 工作流调度 | 实现 `IWorkflowScheduler` |
| 访问上下文 | 提供 `Access` 与 `AccessContextItemProvider` |

### 6.1 注册入口

这些扩展最终都通过 `DI` 注册，主入口在：

```csharp
services.AddAppSchemaAssemblies(typeof(Program).Assembly);
```

以及按需追加的：

```csharp
services.AddAppDataProvider<MySqlAppDataProvider>();
services.AddSchemaStorageProvider<DynamicAppEntryStorageProvider>();
services.WithSchemaApiProtocol<JsonRpcSchemaApiProtocol>();
app.UseSchemaApis(enableAppDataApi: true, enableSchemaManage: true);
```

见 [AppService.cs](../../../SchemaNode.App/Service/AppService.cs#L18-L64) 与 [Service.cs](../../../SchemaNode.App/Http/Service.cs#L33-L130)。

### 6.2 一个重要的扩展原则

App 方言的扩展不是“加一个钩子”，而是“加一个语义声明”。

例如新增一个工作流节点：

```csharp
[Meta<SchemaType>($"{NS_SYSTEM_WORKFLOW_CONTROL}.my_node")]
[Meta<OfNodeKind>(NODE_KIND_WORKFLOW)]
public class MyNode: BaseWorkflow
{
    public async Task ProcessAsync(WorkflowContext context, string arg)
    {
        ...
        SetPayload(context, result);
    }
}
```

不需要修改编辑器、不需要注册路由、不需要写配置。`WorkflowGenerator` 会把它生成为 `node.workflow` 类型，编辑器按通用规则渲染。

这正是第 01 章所说的：**执行能力本身也可以成为语义。**

---

## 7. 全链路回顾

把十章串起来，一个完整的请求大致是这样流动的：

```text
HTTP
  ↓
SchemaApi（解析、校验、时区/locale）
  ↓
SchemaContext
  ├── AuthorizeAsync（Auths / PolicyScope）
  ├── GetAppTypeAsync（AppType / AppFieldType）
  └── PrepareFieldDataAsync（DynamicTableSchema）
        ↓
IAppDataProvider
  ├── Query  ← AppSchemaDataFilter（前端 + RowAuths）
  ├── Save   ← PushAppData / 派生推送
  └── Delete
        ↓
Commit
  ├── ProcessDataPush（DataDerive / 观察者）
  ├── Commit
  └── RaiseEvent
        ↓
Event → WaitEvent → WorkflowContext → 节点 → Function
```

而定义侧的闭环是：

```text
编辑 / 导入
  ↓
IAppEntryStorageProvider
  ↓
AppSchemaRuntime
  ↓
AppType / AppFieldType / AppWorkflowType
  ↓
投影（GetSchemaAsync）→ 编辑器
```

---

## 8. 结语

App 方言的价值不在于它提供了多少 API，而在于它把常规应用中的大部分可变部分——数据模型、权限、视图、派生、流程、事件——都变成了可描述、可组合的语义结构。

```text
Core        → 公共语义与定义域
App         → 一种可执行方言
业务        → 组合这些能力
执行层      → Provider / Scheduler / Dispatcher
```

它既不是唯一实现，也不是最终形态。理解了这套代码如何把“语义”投影为“执行”，就可以按同样的方式构建自己的方言，或者替换其中任何一层。

> 代码是这套设计的当前实现，而不是边界。真正稳定的是“定义 → 加载 → 执行”的结构，以及各层之间最小化的契约。
