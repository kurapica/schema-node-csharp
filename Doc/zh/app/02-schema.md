# 02. 应用语义定义

上一章给出了 App 方言的地图。本章进入定义层：应用、字段、工作流究竟以什么形式存在、保存在哪里、又如何被组合出来。

阅读重点不是字段清单，而是三个问题：

1. 为什么 App 的定义体这么“薄”？
2. 定义如何从外部来源进入运行时？
3. 多个来源的同名定义如何合并？

---

## 1. 定义体只描述身份，语义挂在 Property 上

`AppSchema`、`AppFieldSchema`、`AppWorkflowSchema` 都继承自 `PropertyOwner`。这意味着一部分语义直接写在定义体的属性里（身份、结构），另一部分语义以 Property 形式挂载。

```text
AppSchema
 ├── Container / Name      （身份，Meta）
 ├── Apps / Fields / Workflows / NodeSchemas （运行期状态，SchemaIgnore）
 └── Property
      ├── Display
      ├── Description
      ├── Relations
      ├── SystemDefined
      ├── ScopePolicy
      └── Auths / Data* / Schema*
```

这种拆分让“定义”保持稳定，而方言能力可以持续追加。例如权限相关的 `Auths`、`DataCreate` 等属性，都不需要修改 `AppSchema` 本身的字段。

### 1.1 AppSchema 的身份与容器

见 [AppSchema.cs](../../../SchemaNode.App/Schema/App/AppSchema.cs#L24-L58)：

```csharp
[Meta<SchemaKind>(SCHEMA_KIND_APP, SCHEMA_KIND_ORDER_APP)]
public sealed class AppKind;

[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP}.schema")]
[Meta<Append>(typeof(Display), typeof(Description), typeof(Relations), typeof(SystemDefined))]
[Meta<Attach>(SCHEMA_KIND_APP)]
public sealed class AppSchema: PropertyOwner, IErrorProvider
{
    [Meta<PrimaryIndex>(0)]
    [Meta<SchemaType>(typeof(AppType))]
    public string? Container { get; set; }

    [Meta<PrimaryIndex>(1)]
    [Meta<SchemaType>(typeof(Identifier))]
    public required string Name { get; set; }
}
```

- `PrimaryIndex` 让应用在存储层拥有复合主键 `(Container, Name)`；
- `Container` 的类型是 `AppType`，因此父应用是一个合法的应用引用；
- `Append` 决定“应用可以拥有哪些公共属性”，`Attach` 决定这些属性可以挂到 `app` 这个 Kind 上；
- `SystemDefined` 用来区分系统自动生成的应用与用户应用。

`AppSchema` 中的 `Apps`、`Fields`、`Workflows`、`NodeSchemas` 都标了 `SchemaIgnore`，它们不是可编辑的语义字段，而是加载结果：

```text
AppSchema
  ├── Apps         → 子应用集合（由 Provider 递归加载）
  ├── Fields       → 字段集合（按 Seqno 排序）
  ├── Workflows    → 工作流集合
  └── NodeSchemas  → 随应用一起导入/导出的类型
```

### 1.2 应用字段：为什么 Type 一般是 array

见 [AppFieldSchema.cs](../../../SchemaNode.App/Schema/App/AppFieldSchema.cs#L22-L61)：

```csharp
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP_FIELD}.schema")]
[Meta<SchemaKind>(SCHEMA_KIND_APP_FIELD, SCHEMA_KIND_ORDER_APP_FIELD)]
[Meta<TypeProvider>(nameof(Type))]
[Meta<EntrySourceProvider>($"{NS_SYSTEM_SCHEMA_REFLECT_ARRAY}.{nameof(Function.Reflect.Array.getelementaccessentries)}", $"@{nameof(Type)}", NODE_SELF)]
[Meta<AccessValueTypeProvider>($"{NS_SYSTEM_SCHEMA_REFLECT_ARRAY}.{nameof(Function.Reflect.Array.getelementaccessvaluetype)}", $"@{nameof(Type)}", NODE_SELF)]
[Meta<AppProvider>(nameof(App))]
public sealed class AppFieldSchema: PropertyOwner, IErrorProvider
{
    public string App { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
}
```

这里有四个关键 Meta：

| Meta | 作用 |
| --- | --- |
| `TypeProvider` | 指定 `Type` 是字段的值类型来源 |
| `EntrySourceProvider` | 通过数组的 `getelementaccessentries` 获取字段内部的访问路径 |
| `AccessValueTypeProvider` | 通过数组的 `getelementaccessvaluetype` 获取元素的访问值类型 |
| `AppProvider` | 指定 `App` 是所属应用，用于权限继承和路径解析 |

因此 `Type` 通常是 `array<struct<...>>`。数组元素提供主键与字段结构，字段本身代表了“这些记录的集合”。这也解释了为什么 `DataDerive`、`View`、`Foreigns` 等属性都工作在数组元素层面。

`App` 和 `Name` 分别被标记为 `PrimaryIndex(0)`、`PrimaryIndex(1)`，字段在应用内唯一，且 `Name` 不可变（`Immutable`）。

### 1.3 工作流节点：图结构写在定义里

[AppWorkflowSchema.cs](../../../SchemaNode.App/Schema/App/AppWorkflowSchema.cs#L27-L177) 的关键不是节点字段，而是几条关系：

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

- `Previous` 声明前驱节点，运行时据此连图；
- `Payload` 带 `DisplayOnly`，并由 `Relation` 从 `Type` 推导，用户不能直接填；
- `Fork` / `ForkKey` 只在节点类型支持分叉时可见；
- `State` 的类型由节点类型的 `getstatetype` 推导。

`AppWorkflowSchema` 本身还声明了 `EntrySourceProvider`，让访问路径沿着 `Previous` 和 `Nodes` 展开。这是 SchemaNode 中“路径即语义”的一个典型用法：节点图的访问路径不是特例代码，而是由关系声明出来的。

---

## 2. 事件与工作流节点类型也是 Schema

App 方言不仅提供 `app.*` 的 SchemaKind，还提供 `node.event` 与 `node.workflow`。它们是“节点类型”的定义。

### 2.1 事件声明

[EventSchema.cs](../../../SchemaNode.App/Schema/Type/EventSchema.cs#L19-L41)：

```csharp
[Meta<SchemaKind>(SCHEMA_KIND_NODE_EVENT, SCHEMA_KIND_ORDER_EVENT)]
[Meta<NodeKind>(NODE_KIND_EVENT, SCHEMA_KIND_ORDER_EVENT)]
[Meta<NodeType>(typeof(RuntimeEventType))]
[Meta<SchemaGenerator>(typeof(EventGenerator))]
[Meta<Append>(typeof(Generics), typeof(Relations))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_EVENT}.schema")]
public sealed class EventSchema: PropertyOwner
{
    public FuncArg[]? Args { get; set; } = [];
    public string? Payload { get; set; }
}
```

事件类型在 C# 中声明，由 `EventGenerator` 生成 Schema，`Payload` 指定负载类型。`Args` 用于构造主题（Topic），这一点在事件章节展开。

### 2.2 工作流节点类型

[WorkflowSchema.cs](../../../SchemaNode.App/Schema/Type/WorkflowSchema.cs#L19-L59)：

```csharp
[Meta<SchemaKind>(SCHEMA_KIND_NODE_WORKFLOW, SCHEMA_KIND_ORDER_WORKFLOW)]
[Meta<NodeKind>(NODE_KIND_WORKFLOW, SCHEMA_KIND_ORDER_WORKFLOW)]
[Meta<NodeType>(typeof(RuntimeWorkflowType))]
[Meta<SchemaGenerator>(typeof(WorkflowGenerator))]
public sealed class WorkflowSchema: PropertyOwner
{
    public string Kind { get; set; }
    public string? Payload { get; set; }
    public string? Settings { get; set; }
    public string? Session { get; set; }
    public FuncArg[]? Args { get; set; } = [];
}
```

五个语义位分别对应：

```text
Kind      → 节点类别（workflow / call / event / interaction）
Payload   → 节点输出类型
Settings  → 工作流创建时的全局配置类型
Session   → 节点执行期间的会话状态类型
Args      → 节点参数
```

工作流节点类型与普通类型不同：它和执行的耦合更紧，因此通常由 C# 声明，再通过 Generator 暴露给前端。

---

## 3. 定义从哪里来

App 不要求定义一定存放在数据库。定义的来源通过两个接口隔离：

```text
IAppEntryProvider
  ├── GetSchemaAsync(names)        （NodeSchema）
  ├── GetEnumEntryAccessAsync      （枚举子项）
  └── GetAppSchemaAsync(app)       （AppSchema）

IAppEntryStorageProvider : IAppEntryProvider
  ├── SaveSchemaAsync / DeleteSchemaAsync
  ├── SaveEnumEntriesAsync
  ├── SaveAppSchemaAsync / DeleteAppSchemaAsync
  ├── SaveAppFieldSchemaAsync / DeleteAppFieldSchemaAsync / SwapAppFieldSchemaAsync
  └── SaveAppWorkflowSchemaAsync / DeleteAppWorkflowSchemaAsync
```

见 [IAppEntryProvider.cs](../../../SchemaNode.App/Schema/Provider/IAppEntryProvider.cs) 与 [IAppEntryStorageProvider.cs](../../../SchemaNode.App/Schema/Provider/IAppEntryStorageProvider.cs#L8-L72)。

这种拆分带来两种部署形态：

- 只实现 `IAppEntryProvider`：定义只读（例如来自文件、远程配置中心、代码生成）；
- 同时实现 `IAppEntryStorageProvider`：定义可在线编辑并持久化。

### 3.1 用应用自己的数据存储应用定义

App 自带的 [DynamicAppEntryStorageProvider.cs](../../../SchemaNode.App/Schema/Provider/DynamicAppEntryStorageProvider.cs#L403-L418) 把 Schema 存进 `IAppDataProvider`，通过 Entity 访问：

```csharp
static readonly string Target = Guid.Empty.ToString("D");

public async Task<bool> SaveAppSchemaAsync(AppSchema app)
{
    await context.BeginTransactionAsync();
    await context.SaveEntityAsync<AppEntity>(Target, app!);
    await context.CommitTransactionAsync();
    return true;
}
```

三个要点：

1. 它不直接写 SQL，而是复用 `SchemaContext` 的 Entity 能力；
2. `Target` 固定为 `Guid.Empty`，表示系统作用域，与业务租户的数据隔离；
3. 加载时按 `Container` 递归查询子应用、字段和工作流，并按 `Seqno` 排序，见 [GetAppSchemaAsync](../../../SchemaNode.App/Schema/Provider/DynamicAppEntryStorageProvider.cs#L334-L400)。

`IAppEntryStorageProvider` 同时继承 `IAppEntryProvider`，所以 `AddSchemaStorageProvider<T>()` 会一次性注册两件事：

```csharp
services.TryAddScoped<IAppEntryStorageProvider>(sp => sp.GetRequiredService<T>());
return services.AddAppSchemaProvider<T>();
```

见 [AppService.cs](../../../SchemaNode.App/Service/AppService.cs#L25-L30)。

---

## 4. 一次保存走完的链路

保存不是简单地“写库”。以 `SaveAppSchemaAsync` 为例，见 [SchemaStorageProviderExtension.cs](../../../SchemaNode.App/Schema/Provider/SchemaStorageProviderExtension.cs#L178-L263)：

```text
SaveAppSchemaAsync
  ├── ClearPolicyFlags          （清除前端传入的权限标记，防止越权）
  ├── GetAppTypeAsync           （取现有运行时对象）
  ├── AuthorizeAsync            （SchemaCreate / SchemaUpdate）
  ├── ScopePolicy 变更检查      （非 DEBUG 下不允许中途改目标策略）
  ├── 保存 NodeSchemas          （字段类型可能依赖这些类型）
  ├── provider.SaveAppSchema    （持久化）
  ├── 父应用 SaveAppSchema      （运行时挂到应用树）
  ├── GetAppTypeAsync(reload)   （强制重载）
  ├── 逐个保存 Fields
  ├── 逐个保存 Workflows
  └── RaiseEvent<AppSchemaChangeEvent>
```

这条链路体现了 App 方言的两个原则：

- **权限先行**：所有写操作都先经过 `AuthorizeAsync`；
- **定义与运行时同步**：持久化后立刻重载，避免出现“库里有、内存没有”的状态。

字段保存还多一层校验，见 [SaveAppFieldSchemaAsync](../../../SchemaNode.App/Schema/Provider/SchemaStorageProviderExtension.cs#L293-L327)：

```csharp
NodeType fieldType = await context.GetNodeTypeAsync(field.Type) ?? throw ...;
if (fieldType is Runtime.ArrayType arrType) fieldType = arrType.Element ?? throw ...;
if (fieldType is Runtime.StructType structType)
{
    if (!structType.GetFields().Any()) throw ...;
    if (node.ScopePolicy?.ContextMaps is { Length: > 0 })
    {
        if (structType.GetFields().Any(f => node.ScopePolicy.ContextMaps.Any(m => f.Name.Equals(m.MapKey))))
            throw ...;
    }
}
```

这说明字段类型不是任意类型：它必须能解析、数组必须有元素、结构体不能为空，并且不能与应用的隔离上下文字段冲突。

---

## 5. 多来源合并

应用定义可以来自多个 Provider。`AppSchema.Combine` 负责合并，见 [AppSchema.cs](../../../SchemaNode.App/Schema/App/AppSchema.cs#L127-L163)：

```csharp
public bool Combine(AppSchema? other, ISchemaRuntime? runtime = null)
{
    if (other is null || !other.Name.Equals(Name, StringComparison.OrdinalIgnoreCase)) return false;
    CombineProperties(other, runtime, SCHEMA_KIND_APP);

    // 子应用：同名则递归合并，新应用直接追加
    if (other.Apps is { Length: > 0 })
    {
        if (Apps == null || Apps.Length == 0) Apps = other.Apps.ToArray();
        else Apps = Apps.Concat(other.Apps.Where(a => !Apps.Any(e => e.Combine(a))).ToArray()).ToArray();
    }

    // 字段与工作流的合并规则相同，见源码 L145-L161
}
```

`AppFieldSchema.Combine` 额外要求 `Type` 一致，见 [AppFieldSchema.cs](../../../SchemaNode.App/Schema/App/AppFieldSchema.cs#L80-L86)：

```csharp
if (other is null || !other.Name.Equals(Name, StringComparison.OrdinalIgnoreCase)) return false;
if (other.Type.Equals(Type, StringComparison.OrdinalIgnoreCase))
    CombineProperties(other, runtime, SCHEMA_KIND_APP_FIELD);
return true;
```

也就是说，同名字段可以叠加属性，但不能悄悄改变类型。工作流的 `Combine` 更严格，只允许同名，见 [AppWorkflowSchema.cs](../../../SchemaNode.App/Schema/App/AppWorkflowSchema.cs#L74-L77)。

这为“基础应用 + 租户覆盖 + 插件扩展”提供了组合机制，而不需要把定义做成一个大配置。

---

## 6. 系统应用与自动生成

系统应用不是手写的。`AppRuntimeStageHandler.OnSystemSchemaLoaded` 会扫描程序集里的类型，凡标注了 `[App]` 的，就生成对应的应用与字段：

```csharp
if (runtime.GetTypeSchema(type) is { } schemaType &&
    type.GetMetaProperty<App>() is { HasValue: true } app)
{
    string appName = app.Value!.ToLowerInvariant();

    AppSchema appSchema = new AppSchema
    {
        Name = appName.GetSchemaName(),
        Container = appName.GetNamespace(),
    };
    ...
    runtime.SaveSystemAppSchema(appSchema);
}
```

见 [AppRuntimeStageHandler.cs](../../../SchemaNode.App/Service/AppRuntimeStageHandler.cs#L119-L196)。

生成过程中有两个细节值得注意：

1. **字段类型优先取数组**：`runtime.GetSystemArraySchema(schemaType, true) ?? schemaType`。如果类型有主键，就自动包成数组字段，否则直接用标量/结构体。
2. **`[DataDerive]` 自动补齐 Combine**：如果字段类型是结构体，会把结构体字段上的 `[DataCombine]` 收集成 `Derive.Combines`，见同文件 `L166-L193`。

这让“用 C# 声明一个系统应用”成为可行的开发方式，而不必写 JSON。

---

## 7. JSON 导入导出与校验

定义最终会以 JSON 在环境之间迁移。序列化必须保留扩展属性（`display`、`struct`、`enum`、`func` 等），因此不能使用默认序列化配置。项目在 [SchemaNodeJsonExtension.cs](../../../SchemaNode.App/Schema/SchemaNodeJsonExtension.cs#L13-L44) 中暴露了与 HTTP API 一致的序列化入口：

```csharp
public static JsonSerializerOptions GetSerializerOptions() => JsonOptions.GetJsonOptions(false);

public static NodeSchema? ParseNodeSchema(this SchemaContext context, JsonNode? node)
    => context.FromJsonRequest<NodeSchema>(node.ToJsonString());

public static JsonNode? ToSchemaJson(this SchemaContext context, NodeSchema? schema)
    => schema?.ToJsonNode();
```

外部工具（例如 AI 对话、MCP 工具、`validate-schema` API）可以使用 [SchemaCandidateValidator.cs](../../../SchemaNode.App/Schema/Validation/SchemaCandidateValidator.cs#L24-L94) 做统一校验。它不是第二套解析器，而是复用运行时类型定义：

```csharp
await ValidateCoreAsync(context, normalized, expectedName, checkAuthorization, result, 0, cancellationToken);
```

校验前还会做三类规范化：

- `NormalizeLocaleStrings`：把字符串形式的显示名转成 locale 对象；
- `NormalizeTypeAliases`：把 `string`、`int` 等短名转换为 `system.string` 等规范类型名；
- `NormalizeFlattenedDefinitions`：兼容扁平化的扩展属性。

这让“模型生成的 Schema”和“编辑器保存的 Schema”走同一套规则。

---

## 8. 小结

App 的定义层可以概括为：

```text
身份（Meta） + 结构（字段） + 语义（Property） + 来源（Provider） + 合并（Combine）

AppSchema        → 应用容器，可嵌套
AppFieldSchema   → 数组形式的逻辑数据对象
AppWorkflowSchema→ 有向无环图 + 节点类型
EventSchema      → 事件声明（node.event）
WorkflowSchema   → 工作流节点类型（node.workflow）
```

下一章进入运行期：这些定义如何被加载成 `AppType`、`AppFieldType`、`AppWorkflowType`，以及它们如何向外部投影 Schema。
