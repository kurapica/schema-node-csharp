# 03. 运行时加载

定义只是文本和结构。要让它们参与执行，必须先被加载为运行期对象。

本章沿 `SchemaContext.GetAppTypeAsync` 的调用链，说明 App 如何把 `AppSchema` 变成 `AppType`、`AppFieldType`、`AppWorkflowType`，以及加载过程中哪些信息会被固化、哪些会被缓存、哪些会被投影出去。

---

## 1. 两棵树

Core 的 `SchemaRuntime` 维护类型树：命名空间、类型、枚举、关系。

App 的 `AppSchemaRuntime` 继承它，额外维护应用树：

```text
SchemaRuntime
  ├── RootNamespace
  ├── NodeTypes（类型）
  └── ...

AppSchemaRuntime : SchemaRuntime
  ├── _rootAppSchema    → 系统应用树（[App] 扫描结果）
  ├── _apps             → 系统应用运行期对象
  ├── _appFieldTypes    → 系统字段类型索引
  ├── _typeAppFields    → 类型 → (应用, 字段) 反查
  └── RootAppType       → 用户应用树的根
```

见 [AppSchemaRuntime.cs](../../../SchemaNode.App/Context/AppSchemaRuntime.cs#L16-L24)。

两个根是刻意分开的：

- `_rootAppSchema` / `_apps` 只承载系统应用，来自 `[App]` 标注；
- `RootAppType` 承载用户定义的应用，来自 Provider 与存储。

`GetSystemAppField(Type)` 提供了“类型 → 系统字段”的反查，AI 工具或数据推送可以用它判断某个 C# 类型对应哪个应用字段。

---

## 2. 应用的加载入口

`GetAppTypeAsync` 是一个按命名空间逐段下钻的加载器，见 [SchemaContext.cs](../../../SchemaNode.App/Context/SchemaContext.cs#L23-L71)：

```csharp
public async Task<RuntimeAppType?> GetAppTypeAsync(string fullName, bool reload = false)
{
    AppSchemaRuntime runtime = context.Runtime as AppSchemaRuntime ?? throw ...;
    SpanReader spans = fullName;
    RuntimeAppType? app = await LoadAppTypeAsync(runtime.RootAppType, spans);
    while (app != null && spans.NextPath())
        app = await LoadAppTypeAsync(app, spans);
    return app;
}
```

`fullName` 例如 `activity.activity`，会先加载 `activity`，再加载 `activity.activity`。每一段都经过同一套逻辑：

```text
LoadAppTypeAsync(root, span)
  ├── 已加载且非 reload   → 直接返回
  ├── LoadAppSchemaAsync  → 获取 AppSchema
  ├── 创建 RuntimeAppType
  ├── 先把 schema.Apps 摘掉，避免递归加载
  ├── 标记 Loaded = true
  ├── result.LoadAsync(context, schema)
  └── 把子应用 schema 挂回容器
```

“先摘掉子应用再加载”是为了避免加载父应用时递归触发所有子应用。

### 2.1 Schema 来源的优先级

`LoadAppSchemaAsync` 决定一个应用的 Schema 从哪里来，见 [SchemaContext.cs](../../../SchemaNode.App/Context/SchemaContext.cs#L73-L113)：

```text
1. 系统应用（_rootAppSchema）          → LoadState = System
2. context.SystemMode == true          → 只用系统应用
3. 每个 IAppEntryProvider.GetAppSchemaAsync
       ├── LoadState = Service
       ├── 若已有系统 Schema，则把系统部分合并到 Provider 结果
       └── 合并策略：loadSchema.Combine(schema)
```

得到的结果按 LoadState 标记来源，后续重载会据此决定是否跳过系统部分。

这也解释了为什么 App 的定义可以同时来自代码（系统应用）、数据库（Dynamic provider）和第三方 Provider，而不会互相覆盖。

---

## 3. AppType 的加载

`AppType.LoadAsync` 见 [AppType.cs](../../../SchemaNode.App/Runtime/App/AppType.cs#L186-L257)：

```csharp
internal async Task LoadAsync(SchemaContext context, AppSchema schema)
{
    Release();
    _relations = null;

    _schema = schema;
    _props = schema.GetProperties(context.Runtime.GetSchemaKindPropertyTypes(SCHEMA_KIND_APP)).ToArray();

    (_refTypes, Error) = await schema.LoadPropertiesAsync(context, _props);
    ScopePolicy = GetProperty<ScopePolicy>()?.Value;

    _fields = schema.Fields?.Select(f => new AppFieldType(this, f)).ToList() ?? [];
    foreach (AppFieldType appFieldType in _fields)
    {
        await appFieldType.LoadAsync(context);
        Error ??= appFieldType.Error;
    }
    ...
}
```

加载顺序很重要：

```text
Release()
  ↓  释放旧的 UsedBy 关系，避免类型无法重载
Props
  ↓  LoadPropertiesAsync 解析属性值及引用类型
ScopePolicy
  ↓  应用的目标策略（SystemLevel / BusinessTarget / IsolationContext）
Fields
  ↓  每个字段独立加载
Relations
  ↓  应用级关系（跨字段约束）
Workflows
  ↓  复用已激活的实例，否则进队列
ReferenceTypes → AddUsedBy
```

几个关键机制：

### 3.1 UsedBy 与重载

`AppType` 实现了 `INodeTypeReLoadHandler`。当引用类型变化时，运行时通过 `AddUsedBy(this)` 找到依赖它的应用，再触发重载。

```csharp
foreach (NodeType t in GetReferenceTypes())
    t.AddUsedBy(this);

public void OnNodeTypeLoaded(NodeType[] types)
{
    if (_fields != null)
        foreach (AppFieldType f in _fields)
            if (types.Any(t => t == f.ValueType))
                f.ResetItems();
}
```

见 [AppType.cs](../../../SchemaNode.App/Runtime/App/AppType.cs#L255-L281)。

`ResetItems` 会清空字段上缓存的编译结果（例如动态表结构、推送函数编译结果），但保留字段对象本身，避免整棵树重建。

### 3.2 工作流的延迟激活

```csharp
foreach (var wf in _workflows ?? [])
{
    if (context.Runtime.Stage == RuntimeStage.Activated)
        await wf.LoadAsync(context);
    else
        (context.Runtime as SchemaRuntime)?.GetOrAddRuntimeItem<AppWorkflowQueue>().Enqueue(wf);
}
```

启动阶段尚未 `Activated` 时，工作流先入队；`AppRuntimeStageHandler.OnActivatingAsync` 再统一加载。这保证工作流不会在运行时尚未就绪时启动。

### 3.3 应用范围策略

`ScopePolicy` 决定应用的目标维度：

| 类型 | 含义 |
| --- | --- |
| `SystemLevel` | 全局数据，无 target |
| `BusinessTarget` | 以某个业务对象为目标（默认） |
| `IsolationContext` | 以租户/组织等上下文字段隔离 |

`GetScopeContextItemTypes` 会把 `ContextMaps` 转换成实际的上下文字段，并为缺失的 target 补上默认目标字段，见 [AppType.cs](../../../SchemaNode.App/Runtime/App/AppType.cs#L465-L532)。

---

## 4. AppFieldType 的加载

字段加载是 App 运行时最复杂的一段。`LoadAsync` 见 [AppFieldType.cs](../../../SchemaNode.App/Runtime/App/AppFieldType.cs#L223-L403)，可以按顺序分为六步：

```text
1. ValueType           → 解析 Type
2. Properties          → 解析字段属性，得到 _refTypes / Error
3. Storage             → Disable / EnableStorage / Topology / TableName / Pageable / Filters
4. Primary             → 若为结构体数组，解析主键对应的 C# 属性
5. DataDerive          → 解析推送函数、观察者、第三方字段
6. Foreigns / View     → 校验外键与视图
```

### 4.1 存储相关属性

```csharp
Disable = GetProperty<Disable>()?.Value;
EnableStorage = GetProperty<EnableStorage>()?.Value ?? false;
Topology = GetProperty<Topology>()?.Value;
TableName = GetProperty<TableName>()?.Value;
AttrTableName = GetProperty<AttrTableName>()?.Value;
Pageable = GetProperty<Pageable>()?.Value;
Filters = GetProperty<Filters>()?.Value;

public bool EnableDynamicTable => Disable != true && EnableStorage;
```

`EnableDynamicTable` 是后续数据层的开关：字段只有在启用存储且未禁用时，才会创建动态表。

### 4.2 主键

```csharp
if (ValueType is ArrayType arr && arr.Primary is { Count: > 0 } && structType != null 
    && structType.GetProperty<SystemDefined>()?.Value == true 
    && structType.GetCsharpType() is { } ctype)
{
    _primarys = [];
    foreach (string primary in arr.Primary)
        _primarys.Add(ctype.GetProperty(primary, ...));
}
```

只有“系统定义的结构体”（C# 声明的类型）才解析 C# 主键；用户在线定义的类型没有对应的 C# 类型，主键通过语义层处理。

### 4.3 数据派生与观察者

```csharp
Derive? derive = GetProperty<DataDerive>()?.Value;
if (!string.IsNullOrWhiteSpace(derive?.Source))
{
    if (await context.GetNodeTypeAsync<FunctionType>(derive.Calc) is { Args.Length: 1 } funcNode)
    {
        PushFunc = funcNode;
        AppFieldType? pushSource = Application.GetField(derive.Source);
        ...
        pushSource.AddObserver(this);
        PushSource = pushSource;

        funcNode.ClearRuntimeFuncCache<DataDeriveCompileContext>();
        DataDeriveCompileContext compileContext = new DataDeriveCompileContext(context, funcNode);
        FunctionTypeSchema pushSchema = await compileContext.VisitFunctionType();
        if (compileContext.ThirdFields.Length > 0)
        {
            ThirdPushFields = compileContext.ThirdFields;
            foreach (DataDeriveThirdFieldInfo push in compileContext.ThirdFields)
                Application.GetField(push.Field)?.AddObserver(this);
        }
        PushFuncSchema = pushSchema;
    }
}
```

见 [AppFieldType.cs](../../../SchemaNode.App/Runtime/App/AppFieldType.cs#L260-L310)。

这段代码建立了血缘链：

```text
源字段 AddObserver(派生字段)
第三方字段 AddObserver(派生字段)
派生字段记录 PushSource / ThirdPushFields / PushFuncSchema
```

后续任何源字段变更，都可以沿观察者链找到需要重算的字段。

### 4.4 外键与视图校验

外键要求引用应用不能是 `SystemLevel`，且被引用字段存在：

```csharp
if (string.IsNullOrWhiteSpace(foreign.Field) || string.IsNullOrWhiteSpace(foreign.App) ||
    await context.GetAppTypeAsync(foreign.App) is not { } refApp ||
    refApp.ScopeType == AppScopeType.SystemLevel ||
    structType?.GetField(foreign.Field) == null)
{
    Error ??= AppErrorCodes.APP_FIELD_FOREIGN_NOT_VALID;
}
```

视图校验更严格：源字段结构必须匹配，`DisplayOnly` 字段要么由关系生成，要么来自源应用的同名字段：

```csharp
if (f.DisplayOnly == true)
{
    RelationType? relation = structType.GetRelations(f.Name).FirstOrDefault(r => r.ForProperty<Default>());
    if (relation is not { Process: CallProcess call } || ...)
        Error ??= AppErrorCodes.APP_FIELD_VIEW_NOT_VALID;
}
else if (sourceStruct.GetField(f.Name) is { Type: not null } sourceFieldMatch)
{
    if (!sourceFieldMatch.Type.IsAssignableTo(f.Type))
        Error ??= AppErrorCodes.APP_FIELD_VIEW_NOT_VALID;
}
```

见 [AppFieldType.cs](../../../SchemaNode.App/Runtime/App/AppFieldType.cs#L317-L402)。

---

## 5. 对外投影

运行期对象并不直接把内部状态暴露给前端。`GetSchemaAsync` 负责生成一份“可编辑、带权限标记”的 Schema。

### 5.1 AppType 的投影

见 [AppType.cs](../../../SchemaNode.App/Runtime/App/AppType.cs#L315-L350)：

```csharp
if (!noAuth)
{
    bool isSystem = GetProperty<SystemDefined>()?.Value == true;
    schema.SetProperty<SchemaCreate, bool>(!isSystem && await context.AuthorizeAsync(this, PolicyScope.SchemaCreate, true));
    schema.SetProperty<SchemaRead, bool>(await context.AuthorizeAsync(this, PolicyScope.SchemaRead, true));
    schema.SetProperty<SchemaUpdate, bool>(await context.AuthorizeAsync(this, PolicyScope.SchemaUpdate, true));
    schema.SetProperty<SchemaDelete, bool>(!isSystem && await context.AuthorizeAsync(this, PolicyScope.SchemaDelete, true));
}
```

- `noAuth = true` 时不计算权限，用于内部导出或系统任务；
- 系统应用不允许创建/删除，因此 `SchemaCreate` / `SchemaDelete` 直接为 `false`。

### 5.2 AppFieldType 的投影

字段投影多了数据权限和列黑名单，见 [AppFieldType.cs](../../../SchemaNode.App/Runtime/App/AppFieldType.cs#L421-L472)：

```csharp
schema.SetProperty<DataCreate, bool>(!isSystem && !ReadOnly && await context.AuthorizeAsync(this, PolicyScope.DataCreate, true));
schema.SetProperty<DataRead, bool>(!isSystem && await context.AuthorizeAsync(this, PolicyScope.DataRead, true));
schema.SetProperty<DataUpdate, bool>(!isSystem && !ReadOnly && await context.AuthorizeAsync(this, PolicyScope.DataUpdate, true));
schema.SetProperty<DataDelete, bool>(!isSystem && !ReadOnly && await context.AuthorizeAsync(this, PolicyScope.DataDelete, true));
```

列权限通过 `GetColPolicies(f.Name)` 逐个字段评估，未通过的直接进入 `BlockColumns`：

```csharp
foreach (StructFieldType f in @struct.GetFields())
{
    bool authorized = true;
    foreach (string evaluator in this.GetColPolicies(f.Name))
    {
        authorized = await context.AuthorizeAsync(evaluator, true);
        if (authorized) break;
    }
    if (authorized) continue;
    ignoreFields.Add(f.Name);
}
schema.SetProperty<BlockColumns, string[]>(ignoreFields.ToArray());
```

`BlockColumns` 是“列级权限”的执行形式：前端隐藏列，后端在查询结果中剔除列。

---

## 6. 工作流的加载与激活

`AppWorkflowType` 分两个阶段。

### 6.1 LoadAsync：解析节点

见 [AppWorkflowType.cs](../../../SchemaNode.App/Runtime/App/AppWorkflowType.cs#L105-L127)：

```csharp
_props = _appWorkflowSchema.GetProperties(...).ToArray();
(_refTypes, Error) = await _appWorkflowSchema.LoadPropertiesAsync(context, _props);

foreach (var node in Nodes)
{
    var (_subRefTypes, err) = await node.LoadPropertiesAsync(context, node.GetProperties(...).ToArray());
    Error ??= err;
    ...
    node.PayloadValueType = await context.GetNodeTypeAsync<ValueType>(node.Payload);
}
```

这一步只解析属性与负载类型，不创建执行实例。

### 6.2 ActiveAsync：建图与恢复

见 [AppWorkflowType.cs](../../../SchemaNode.App/Runtime/App/AppWorkflowType.cs#L213-L327)：

```text
对每个节点：
  ├── 取 WorkflowType 的 C# 类型
  ├── Activator.CreateInstance
  ├── 设置 Application / Name / Fork / ForkKey / ...
  ├── 注入 State
  ├── wNode.LoadAsync(context, node)
  ├── 绑定 Args
  └── 连接 Previous / Next

校验入口节点唯一
  ↓
创建 WorkflowContext
  ↓
尝试从 IWorkflowContextPersistence 恢复 Running 快照
  ↓
否则 Initialize(workflow, entry)
```

两个约束在这里被执行：

- 入口节点必须唯一（`topNodes.Count != 1` 抛错）；
- 已激活的工作流在保存定义时不允许直接修改，见 [SchemaStorageProviderExtension.cs](../../../SchemaNode.App/Schema/Provider/SchemaStorageProviderExtension.cs#L396-L419)。

`Active` 标记会持久化，重启后可以重新激活并恢复上下文。

---

## 7. 重载的触发点

重载不是显式的“刷新”按钮，而是由几处自动触发：

| 触发点 | 行为 |
| --- | --- |
| 保存应用 / 字段 / 工作流 | `GetAppTypeAsync(name, reload: true)` |
| 类型加载完成 | `AppType.OnNodeTypeLoaded` → 字段 `ResetItems` |
| 加载用户 Schema 前 | `OnSchemaLoadingAsync` → `RootAppType.ResetLoadState()` |
| 保存系统应用 | `SaveAppSchema` 标记 `Loaded = false` |

`ResetLoadState` 会递归标记整棵应用树未加载，见 [AppType.cs](../../../SchemaNode.App/Runtime/App/AppType.cs#L172-L177)。它用于区分“系统阶段”和“用户阶段”，避免系统 Schema 与用户 Schema 混在同一棵树上。

---

## 8. 小结

```text
GetAppTypeAsync
  ↓
LoadAppSchemaAsync（系统 → Provider → Combine）
  ↓
AppType.LoadAsync
  ├── 属性 / ScopePolicy
  ├── AppFieldType.LoadAsync
  │     ├── 存储属性 / 主键
  │     ├── DataDerive → 观察者与第三方字段
  │     └── Foreigns / View 校验
  ├── 应用级关系
  └── AppWorkflowType（入队或立即加载）
        ↓
      ActiveAsync（建图、入口校验、上下文恢复）
```

运行时对象的目标不是“复制定义”，而是把定义中的引用解析成可执行的对象：类型、函数、观察者、表结构、节点图。

下一章进入数据层，看这些对象如何落到实际的表和查询上。
