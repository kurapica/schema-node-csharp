# 04. 数据层

运行时把字段解析成 `AppFieldType` 之后，数据最终仍要落到某种存储上。App 方言不绑定具体数据库，而是定义一层很薄的数据访问契约，再由不同 Provider 实现。

本章沿 `SaveFieldDataAsync` 与 `QueryDynamicTableAsync` 两条调用链，说明动态表如何生成、CRUD 如何执行、事务与事件如何衔接。

---

## 1. 数据契约

[IAppDataProvider.cs](../../../SchemaNode.App/Data/IAppDataProvider.cs#L11-L61) 只有六个数据方法与三个事务方法：

```csharp
public interface IAppDataProvider
{
    Task<bool> EnsureDynamicTableAsync(DynamicTableSchema schema);

    Task<(IValueAccess? result, int total)> QueryDynamicTableAsync(DynamicTableSchema schema,
        AppSchemaDataResult type, AppSchemaDataFilter? filter = null, int skip = 0, int take = 0,
        bool desc = false, AppSchemaDataOrder[]? orderBy = null, string? dataField = null, bool forUpdate = false);

    Task<(bool result, IValueAccess? update, IValueAccess? origin)> SaveDynamicTableDataAsync(
        DynamicTableSchema schema, IValueAccess? data, bool canAdd = true, bool onlyAdd = false,
        string[]? overrides = null);

    Task<(bool result, IValueAccess? origin)> DeleteDynamicTableDataAsync(DynamicTableSchema schema, AppSchemaDataFilter? filter);
    Task<(bool result, IValueAccess? origin)> ClearDynamicTableDataAsync(DynamicTableSchema schema);
    Task DropDynamicTableAsync(DynamicTableSchema schema);

    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}
```

设计上有两个刻意的选择：

1. **输入输出都是 `SchemaNode.Node` 的值节点**，而不是 C# POCO。Provider 不需要知道业务类型，只需要处理结构；
2. **没有“按 SQL 查询”的方法**。查询条件统一表达为 `AppSchemaDataFilter`，由 Provider 自行翻译。

返回值统一为 `(result, update, origin)`：

- `update` 是本次写入后的数据；
- `origin` 是写入前的数据。

这为后续的事件、派生计算和血缘追踪提供了“变更前 / 变更后”两份快照。

---

## 2. 动态表结构

`DynamicTableSchema` 是字段数据在存储侧的投影，由 [DynamicTable.cs](../../../SchemaNode.App/Data/DynamicTable.cs#L36-L427) 生成。

### 2.1 两类表

```csharp
if (appFieldType is { EnableStorage: false, IsForeignView: false })
{
    Single = true;
    Fields = [];
    return;
}
```

- `Single = true`：单记录表。字段不是数组时，整个值作为一条记录；
- `Single = false`：多记录表。字段是带主键的结构体数组时，每条元素一行。

字段是否真正落库由 `EnableStorage` 决定，视图字段（`IsForeignView`）不建表，直接复用源字段的表。

### 2.2 作用域字段

```csharp
foreach ((string item, ValueType type, bool isTarget) in targetApp.GetScopeContextItemTypes(context))
{
    fields.Add(new DynamicTableField
    {
        Name = item,
        Type = info.Type,
        MaxLength = info.MaxLength,
        ValueType = type,
        Scope = true,
        Target = isTarget
    });
}
```

每个字段表都会自动带上应用的作用域字段（target、租户、组织等）。这使同一张表可以按目标隔离数据，而不需要业务字段自己带 `tenant_id`。

### 2.3 值字段与复合字段

根据字段类型不同，生成规则也不同：

```text
StructType          → 每个结构体字段一列
  ├── 普通字段       → 同名列
  ├── 子结构体       → {field}_{subfield} 复合列
  └── 显示字段       → 尝试生成 Join 列（见下）
ArrayType<Struct>   → 主键列 + 普通列（Single = false）
ArrayType<Scalar>   → _data JSON 列
Scalar / Enum       → _data 单列
```

复合列使用 `COMPLEX_SEP`（`_`）拼接，并通过 `DataFieldComplexInfo` 记录 `Main` / `Field`，读取时再还原成嵌套结构。

### 2.4 显示字段与 Join

`DisplayOnly` 字段不存储，但可以来自其他应用。生成表结构时，会解析该字段上的 `Default` 关系：

```csharp
var relation = structNode.GetRelations(sField.Name)
    .FirstOrDefault(r => r.ForProperty<Default>() &&
                         r.Process is CallProcess call &&
                         IsReferenceFunc(call.Func));

string? app = call.Args.FirstOrDefault()?.Value?.ToValue<string>();
string? field = call.Args.ElementAtOrDefault(1)?.Value?.ToValue<string>();
string? dataField = call.Args.ElementAtOrDefault(2)?.Value?.ToValue<string>();
...
joins.Add(new DynamicTableJoin { Field = field, Matches = keyMap });
```

见 [DynamicTable.cs](../../../SchemaNode.App/Data/DynamicTable.cs#L236-L287)。

生成的 `DynamicTableField` 带 `JoinAppField` / `JoinDataField`，查询时由 Provider 负责关联。这就是“跨表视图字段”能在不复制数据的前提下参与查询的原因。

### 2.5 表名

表名规则在 [AppFieldType.cs](../../../SchemaNode.App/Runtime/App/AppFieldType.cs#L583-L603)：

```csharp
public string DynamicTableName =>
    IsForeignView
    ? View!.AppType?.GetField(View.Field)?.DynamicTableName ?? throw ...
    : !string.IsNullOrWhiteSpace(TableName)
        ? TableName
        : $"{DYNAMIC_TABLE_PREFIX}_{Regex.Replace(App, @"\W+", "_")}_{Name}";
```

- 视图字段复用源字段的表；
- 显式 `TableName` 优先；
- 默认是 `dyn_{app}_{field}`，其中应用名中的非单词字符替换为 `_`。

属性表（EAV）则使用 `eav_` 前缀，只在 `Topology = AttributeBased` 时生成。

---

## 3. 准备表结构

真正执行 CRUD 前，先要确保表存在。入口是 [AppDataTableExtension.cs](../../../SchemaNode.App/Data/AppDataTableExtension.cs#L13-L58)：

```csharp
internal static async Task<DynamicTableSchema> PrepareFieldDataAsync(this SchemaContext context, AppFieldType field)
{
    if (!field.EnableDynamicTable) return field.GetDynamicTableSchema(context);

    DynamicTableSchema? schema = field.GetItem<DynamicTableSchema>();
    if (schema != null) return schema;

    IAppDataProvider dataProvider = context.GetService<IAppDataProvider>() ?? throw ...;
    using ICriticalRegion locker = await context.GetLockAsync($"SCHEMA_CONTEXT_DYN_TABLE_CREATION:{field.DynamicTableName}");
    try
    {
        schema = field.GetItem<DynamicTableSchema>();
        if (schema != null) return schema;

        schema = field.GetDynamicTableSchema(context);
        if (field.IsForeignView)
        {
            AppFieldType foreignField = (await context.GetAppTypeAsync(field.View!.App))?.GetField(field.View.Field) ?? throw ...;
            await context.PrepareFieldDataAsync(foreignField);
        }
        else
        {
            await dataProvider.EnsureDynamicTableAsync(schema);
            if (schema.Joins is { Length: > 0 })
                foreach (DynamicTableJoin join in schema.Joins)
                    await context.PrepareFieldDataAsync(field.Application.GetField(join.Field)!);
        }
        return schema;
    }
    ...
}
```

这段代码解决三个问题：

- **只建一次**：`GetItem<DynamicTableSchema>()` 作为缓存，双重检查；
- **并发安全**：`ICriticalRegionProvider` 按表名加锁，避免并发建表；
- **依赖先行**：视图字段先准备源字段，Join 字段先准备被关联字段。

> 注意 `ICriticalRegion` 不是数据库锁，而是进程内/分布式关键区。它可以替换为 Redis、ZooKeeper 等实现。

---

## 4. CRUD 链

CRUD 的入口是 `SchemaContext` 上的扩展方法，集中定义在 [AppDataTransactionExtension.cs](../../../SchemaNode.App/Data/AppDataTransactionExtension.cs)。

### 4.1 保存

```csharp
public static async Task<bool> SaveFieldDataAsync(this SchemaContext context, AppFieldType field,
    IValueAccess? value = null, bool innerCall = false, bool canAdd = true, bool onlyAdd = false, string[]? overrides = null)
{
    if (!field.EnableDynamicTable || field.IsForeignView) return false;
    if (field.PushSource != null && !innerCall) return false;

    var dataProvider = context.GetService<IAppDataProvider>() ?? throw ...;
    DynamicTableSchema schema = await context.PrepareFieldDataAsync(field);

    (bool result, IValueAccess? update, IValueAccess? origin) =
        await dataProvider.SaveDynamicTableDataAsync(schema, value, canAdd, onlyAdd, overrides);

    if (result) OnFieldDataChanged(context, field, TransactionChangeOperation.Modify, update, origin);
    return result;
}
```

见 [AppDataTransactionExtension.cs](../../../SchemaNode.App/Data/AppDataTransactionExtension.cs#L35-L57)。

三个前置判断值得注意：

| 判断 | 含义 |
| --- | --- |
| `!field.EnableDynamicTable` | 不落库的字段不可写 |
| `field.IsForeignView` | 视图字段只读 |
| `field.PushSource != null && !innerCall` | 派生字段只能由推送流程写入 |

`OnFieldDataChanged` 把变更记录到当前事务的上下文项中，而不是立刻发事件。这是为了避免“事务回滚了，但事件已经发出”。

### 4.2 清除与删除

```csharp
public static async Task<bool> ClearFieldDataAsync(...)
{
    ...
    (bool result, IValueAccess? origin) = await dataProvider.ClearDynamicTableDataAsync(schema);
    if (result) OnFieldDataChanged(context, field, TransactionChangeOperation.DropAll, null, origin);
}

public static async Task<bool> DeleteFieldListDataAsync(this SchemaContext context, AppFieldType field, AppSchemaDataFilter filter)
{
    ...
    (bool result, IValueAccess? origin) = await dataProvider.DeleteDynamicTableDataAsync(schema, filter);
    if (result) OnFieldDataChanged(context, field, TransactionChangeOperation.Delete, null, origin);
}
```

见 [AppDataTransactionExtension.cs](../../../SchemaNode.App/Data/AppDataTransactionExtension.cs#L59-L142)。

`DeleteSchemaNodeAsync`（在 `AppDataProviderExtension` 中）负责把值节点转换为过滤条件，并处理多主键组合：

```text
节点数 <= MAX_COMBINE_CASE_COUNT
  → 每个节点转 AppSchemaDataFilter，再 OrElse 合并

节点数 >  MAX_COMBINE_CASE_COUNT
  → 先按主键收集 IN 列表查询，再在内存中按复合主键精确过滤
```

见 [IAppDataProvider.cs](../../../SchemaNode.App/Data/IAppDataProvider.cs#L71-L194)。

这是为了避免生成过长的 `OR` 条件。

### 4.3 事务与事件

```csharp
public static async Task BeginTransactionAsync(this SchemaContext context)
{
    var dataProvider = context.GetService<IAppDataProvider>() ?? throw ...;
    await dataProvider.BeginTransactionAsync();
    context.SetContextItem(new Dictionary<string, TransactionChangeData>());
}

public static async Task CommitTransactionAsync(this SchemaContext context, bool noEvent = false)
{
    var transChangedData = context.GetOrAddContextItem<Dictionary<string, TransactionChangeData>>();

    // Process data field push
    foreach (string target in transChangedData.Keys.ToArray())
        await ProcessDataPush(context, target, transChangedData[target]);

    await dataProvider.CommitTransactionAsync();

    if (noEvent) return;

    foreach (var (target, value) in transChangedData)
        foreach (var (field, changes) in value.Changes)
            foreach (var change in changes)
                ... RaiseEvent(AppFieldDataCreateEvent/UpdateEvent/DeleteEvent ...)
}
```

见 [AppDataTransactionExtension.cs](../../../SchemaNode.App/Data/AppDataTransactionExtension.cs#L151-L300)。

顺序是固定的：

```text
1. 数据推送      （派生字段先算完）
2. 提交数据库
3. 发出数据事件  （工作流、外部订阅者在此收到通知）
```

这也解释了工作流为什么能基于“数据创建”事件启动：事件只在事务成功后发出。

---

## 5. Entity：用应用数据存应用定义

`Data/Entity` 下的类型是 App 方言的“元数据表”：

| Entity | 对应定义 |
| --- | --- |
| `AppEntity` | `AppSchema` |
| `AppFieldEntity` | `AppFieldSchema` |
| `AppWorkflowEntity` | `AppWorkflowSchema` |
| `NodeEntity` | `NodeSchema` |
| `EnumValueEntity` | 枚举级联子项 |

见 [AppEntity.cs](../../../SchemaNode.App/Data/Entity/AppEntity.cs#L13-L59)：

```csharp
[Meta<App>(NS_SYSTEM_SCHEMA)]
[Meta<EnableStorage>(true)]
internal class AppEntity
{
    [Meta<PrimaryIndex>(0)]
    public string? Container { get; set; }

    [Meta<PrimaryIndex>(1)]
    public string Name { get; set; }

    public JsonObject? Extensions { get; set; }
}
```

显式转换运算符负责 Entity 与 Schema 的互转：

```csharp
public static implicit operator AppEntity?(AppSchema? appSchema) => new AppEntity
{
    Container = string.IsNullOrWhiteSpace(appSchema.Container) ? ROOT : appSchema.Container,
    Name = appSchema.Name,
    Extensions = appSchema.Extensions?.DeepClone() as JsonObject,
};
```

注意 `Extensions` 保存的是全部扩展属性（display、auths、view……），而不是展开成列。这样新增 Property 不需要改表结构。

`EntityExtension` 则把“按主键查询”“按条件查询”包装成 C# 表达式：

```csharp
AppSchemaDataFilter filter = AppSchemaDataFilterVisitor.Build(cond);
using var stack = context.StackAccess(appFieldType.App, target);
var (value, _) = await context.GetAppFieldDataAsync(appFieldType, AppSchemaDataResult.First, filter, ...);
```

见 [EntityExtension.cs](../../../SchemaNode.App/Data/EntityExtension.cs#L22-L66)。

`StackAccess(app, target)` 把目标压入访问上下文，因此 Entity 查询也自动受作用域和目标隔离约束。

---

## 6. Provider 实现

### 6.1 内存实现

[InMemoryAppDataProvider.cs](../../../SchemaNode.App/Data/InMemoryAppDataProvider.cs#L11-L84) 用于测试：

```csharp
ConcurrentDictionary<string, List<IValueAccess>> table = _dynamicTables.GetOrAdd(schema.AppField.DynamicTableName, _ => []);
List<IValueAccess> list = table.GetOrAdd(compositeKey, _ => []);
```

- 用“表名 + 作用域键”作为复合键；
- 过滤直接调用 `filter.Test(schemaContext, node)`；
- 支持 `Count / Exist / First / Last / Field / List` 六种结果形态，与接口语义一致。

它是理解 `IAppDataProvider` 语义的最佳参考实现。

### 6.2 SQL 实现

SQL 数据库通过两个接口组合：

```csharp
public interface IAppDataSqlProvider<T>: IAppDataProvider where T: ISqlProvider;
```

见 [IAppDataSqlProvider.cs](../../../SchemaNode.App/Data/Sql/IAppDataSqlProvider.cs#L1-L7)。

`ISqlProvider` 只负责方言差异：

```text
QuoteField / QuoteTable / QuoteIndex
GenParameterName
Literal
Binary / Arithmetic
LikeContains / LikeStartsWith / LikeEndsWith
In / NotIn / IsNull / IsNotNull
```

见 [ISqlProvider.cs](../../../SchemaNode.App/Data/Sql/ISqlProvider.cs#L11-L80) 与 [DefaultSqlProvider.cs](../../../SchemaNode.App/Data/Sql/DefaultSqlProvider.cs#L8-L59)。

注册时，`AddAppDataProvider<T>` 会自动检测 `IAppDataSqlProvider<>` 并实例化对应的 `ISqlProvider`：

```csharp
Type? interfaceType = typeof(T).GetInterfaces().FirstOrDefault(i => i.IsSubclassOfGenericType(typeof(IAppDataSqlProvider<>)));
if (interfaceType != null)
{
    ISqlProvider instance = (ISqlProvider)Activator.CreateInstance(interfaceType.GetGenericArguments()[0])!;
    services.AddSingleton(instance);
}
```

见 [AppService.cs](../../../SchemaNode.App/Service/AppService.cs#L35-L50)。

---

## 7. 小结

```text
AppFieldType
  ↓
DynamicTableSchema
  ├── 作用域字段
  ├── 主键 / 值字段
  ├── 复合字段
  ├── DisplayOnly → Join
  └── 表名 / EAV 表
  ↓
PrepareFieldDataAsync（关键区 + 依赖递归）
  ↓
IAppDataProvider
  ├── Query   → AppSchemaDataResult
  ├── Save    → (update, origin)
  ├── Delete  → 过滤条件
  └── Clear / Drop
  ↓
CommitTransaction
  ├── ProcessDataPush
  ├── Commit
  └── Raise AppFieldData*Event
```

下一页进入查询与过滤：`AppSchemaDataResult`、`AppSchemaDataFilter` 与行级权限如何合并成最终查询。
