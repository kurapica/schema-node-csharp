# 07. 视图、外键与派生

App 的字段之间不是孤立的。一个字段可以：

- 展示另一个字段的数据（`View`）；
- 把另一个应用的目标作为外键（`Foreigns`）；
- 由源字段计算得出（`DataDerive`）。

三者都在定义层声明，在加载期建立依赖，在数据变更时沿依赖传播。本章沿这三条链展开。

---

## 1. 三种机制的分工

```text
View
  只读展示，不复制数据，复用源表

Foreigns
  引用另一个应用的目标，参与查询/校验

DataDerive
  从源字段计算并写入自己的表，支持跨字段血缘
```

它们解决不同的问题，因此约束也不同：

| 机制 | 写入存储 | 跨应用 | 变更传播 |
| --- | --- | --- | --- |
| View | 否（复用源表） | 是 | 无，实时读取 |
| Foreigns | 是（外键列） | 是 | 无 |
| DataDerive | 是（结果表） | 是 | 观察者链 + 增量推送 |

---

## 2. View：跨应用视图字段

### 2.1 定义

[View.cs](../../../SchemaNode.App/Property/App/View.cs#L21-L77)：

```csharp
[Meta<Static>(true)]
[Relation<InVisible, Call>(nameof(View), NS_SYSTEM_LOGIC_EQ, $"@{nameof(EnableStorage)}", true)]
[Relation<Default, Call>($"{nameof(View)}.{nameof(FieldView.Owner)}", ..., $"@{nameof(App)}")]
public class View : Property<FieldView>;

public sealed class FieldView
{
    public string Owner { get; set; }
    public string App { get; set; }
    public string Field { get; set; }
    public string? FieldType { get; set; }
    public string? Map { get; set; }
}
```

几个约束直接写在关系上：

- `InVisible`：`EnableStorage == true` 时视图配置不可见。视图字段不落库，因此必须关闭存储；
- `Owner` 自动取当前应用，用于排除自身字段；
- `Field` 的候选来自 `getappforeignfields(App, Owner)`，即源应用中“可作为视图来源”的字段；
- `FieldType` 自动从源字段推导，用户不可见；
- `Map` 仅在源应用**不是** `SystemLevel` 时可见，用于把源字段映射到当前应用的目标字段名（系统级应用没有 target，不需要映射）。

### 2.2 为什么视图不复制数据

视图字段没有自己的表。`DynamicTableName` 直接返回源字段的表：

```csharp
public string DynamicTableName =>
    IsForeignView
    ? View!.AppType?.GetField(View.Field)?.DynamicTableName ?? throw ...
    : ...
```

见 [AppFieldType.cs](../../../SchemaNode.App/Runtime/App/AppFieldType.cs#L583-L603)。

因此对视图字段的查询最终落在源表上，作用域、目标、权限都以源字段为准。

### 2.3 加载期校验

`AppFieldType.LoadAsync` 会校验视图结构：

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

见 [AppFieldType.cs](../../../SchemaNode.App/Runtime/App/AppFieldType.cs#L335-L402)。

含义是：

- `DisplayOnly` 的字段必须由关系生成，不能手工定义；
- 非 `DisplayOnly` 的字段，如果源字段存在，类型必须兼容。

---

## 3. Foreigns：外键

[Foreigns.cs](../../../SchemaNode.App/Property/App/Foreigns.cs#L19-L51)：

```csharp
[Meta<Static>(true)]
[Relation<InVisible, Relation.Call>(nameof(Foreigns), NS_SYSTEM_LOGIC_NOT, $"@{nameof(EnableStorage)}")]
[Relation<BlackList, Relation.Call>($"{nameof(Foreigns)}.{nameof(Foreign.App)}",
    NS_SYSTEM_COLLECTION_NEWARRAY, $"@{nameof(AppFieldSchema.App)}")]
public class Foreigns : Property<Foreign[]>;

public sealed class Foreign
{
    [Meta<SchemaType>(typeof(AppType))]
    public string App { get; set; }

    [Meta<SchemaType>(typeof(Identifier))]
    public string Field { get; set; }
}
```

- 外键必须落库，因此 `EnableStorage = false` 时不可见；
- `App` 的黑名单排除当前应用，防止自引用；
- `Field` 指向另一个应用的**目标字段**。

加载期校验：

```csharp
if (string.IsNullOrWhiteSpace(foreign.Field) || string.IsNullOrWhiteSpace(foreign.App) ||
    await context.GetAppTypeAsync(foreign.App) is not { } refApp ||
    refApp.ScopeType == AppScopeType.SystemLevel ||
    structType?.GetField(foreign.Field) == null)
{
    Error ??= AppErrorCodes.APP_FIELD_FOREIGN_NOT_VALID;
}
```

被引用的应用不能是 `SystemLevel`，因为系统级应用没有 target，无法作为外键目标。

外键在演示中的作用是“为录入提供候选列表”，例如把用户列表、场馆列表作为参会人字段的可选项。它不参与后端的关系运算，这一点在 tryit 中也有说明。

---

## 4. DataDerive：从源字段计算

### 4.1 定义

[DataDerive.cs](../../../SchemaNode.App/Property/App/DataDerive.cs#L34-L144)：

```csharp
[Meta<Static>(true)]
[Relation<InVisible, Call>(nameof(DataDerive), NS_SYSTEM_LOGIC_NOT, $"@{nameof(EnableStorage)})")]
[Relation<Default, Call>($"{nameof(DataDerive)}.{nameof(Derive.SourceType)}",
    $"{NS_SYSTEM_SCHEMA_REFLECT_APP}.{nameof(SystemReflectApp.getappfieldtype)}", $"@{nameof(App)}", $"@{nameof(DataDerive)}.{nameof(Derive.Source)}", true)]
public class DataDerive : Property<Derive>;

public class Derive
{
    public string Source { get; set; }
    public string? FieldType { get; set; }
    public string? SourceType { get; set; }

    [Meta<SchemaType>(typeof(FuncType))]
    [Relation<Valid, Assign>(NS_SYSTEM_SCHEMA_REFLECT_FUNC_WITH_ARGS, NODE_SELF, $"@{nameof(SourceType)}")]
    [Relation<Valid, Assign>(NS_SYSTEM_SCHEMA_REFLECT_FUNC_WITH_RETURN, NODE_SELF, $"@{nameof(FieldType)}", true)]
    public string Calc { get; set; }

    public DataCombineType? Combine { get; set; }
    public FieldCombine[]? Combines { get; set; }
}
```

四个语义位：

```text
Source       源字段（同应用内）
Calc         计算函数，参数类型 = SourceType，返回类型 = FieldType
Combine      标量/枚举的聚合方式
Combines     结构体逐字段的聚合方式
```

`Calc` 的类型约束不是装饰，而是编译期真的会被检查：参数必须能接收源字段，返回值必须能赋给目标字段。

### 4.2 聚合方式

`DataCombineType` 的语义在 [DataDerive.cs](../../../SchemaNode.App/Property/App/DataDerive.cs#L153-L237) 中实现：

```csharp
DataCombineType.Newest → 取最后一条
DataCombineType.Oldest → 取第一条
DataCombineType.Sum    → 数值求和
DataCombineType.Count  → 计数
```

对结构体数组，`GroupJoinObjectMap` 会按主键归并：

```csharp
if (keyMap.TryGetValue(key, out StructNode? total))
{
    keyCount[key]++;
    foreach (string s in valueFields)
    {
        switch (joinMethodMap.GetValueOrDefault(s, DataCombineType.Newest))
        {
            case DataCombineType.Newest: ...
            case DataCombineType.Oldest: ...
            case DataCombineType.Sum: ...
            case DataCombineType.Count: ...
        }
    }
}
```

见 [DataDerive.cs](../../../SchemaNode.App/Property/App/DataDerive.cs#L242-L326)。

> 演示中的“组织人已参加汇总”就是 `Combine = Count`：以 `orgnizer` 为主键，计数参会人。

### 4.3 编译期：血缘从哪里来

派生字段的函数不是“运行时想怎么查就怎么查”。为了让系统知道**什么变化会导致重算**，编译期会分析函数的表达式树。

`DataDeriveCompileContext` 见 [DataDeriveCompileContext.cs](../../../SchemaNode.App/Runtime/Compile/CompileContext/DataDeriveCompileContext.cs#L13-L124)：

```csharp
public class DataDeriveCompileContext(SchemaContext context, FunctionType function) : CompileContext(context, function)
{
    private readonly List<DataDeriveThirdFieldInfo> _thirdFields = [];
    public DataDeriveThirdFieldInfo[] ThirdFields => _thirdFields.ToArray();
    ...
}
```

它做两件事。

**第一，识别第三方字段。**

```csharp
case $"{NS_SYSTEM_DATA_APP}.{nameof(SystemAppData.getfield)}":
{
    if (funcCallExp.ApplyMode == ApplyMode.Call && ...)
    {
        _appType ??= await Context.GetAppTypeAsync(appExp.Value.GetValue<string>()!);
        var thirdField = _appType.GetField(fieldExp.Value.GetValue<string>() ?? string.Empty);
        if (thirdField?.ValueType is not ArrayType { Element: StructType arrayStruct, Primary: { Count: > 0 } } arrayType) break;
        ...
        _thirdFields.Add(thirdFieldInfo);
    }
}
```

只要函数里出现 `system.data.app.getfield('app', 'field', ...)`，第三个字段就被记录为 `DataDeriveThirdFieldInfo`。

**第二，要求主键来自参数或常量。**

```csharp
foreach (StructFieldExp field in toStruct.Fields)
{
    if (array.Primary.Contains(field.Name, StringComparer.OrdinalIgnoreCase))
    {
        bool fromValid = false;
        SchemaExp? keyExp = field.Expression;
        while (keyExp != null)
        {
            switch (keyExp)
            {
                case VariableExp varExp: keyExp = varExp.Value; break;
                case FieldAccessExp fieldAccessExp:
                    SchemaExp owner = fieldAccessExp.Owner;
                    if (owner is ArgumentExp argExp)
                    {
                        keyExp = null; fromValid = true;
                        if (argExp.Index > 0)
                        {
                            DataDeriveThirdFieldInfo fldInfo = _thirdFields.First(a => a.Arg == argExp);
                            if (!fldInfo.PushKeys.Contains(fieldAccessExp.FieldName))
                                fldInfo.PushKeys.Add(fieldAccessExp.FieldName);
                        }
                    }
                    else throw new FunctionVisitException(AppErrorCodes.FUNC_IS_NOT_PUSH_FUNC);
                    break;
                case ConstantExp: keyExp = null; fromValid = true; break;
                default: throw new FunctionVisitException(AppErrorCodes.FUNC_IS_NOT_PUSH_FUNC);
            }
        }
        if (!fromValid) throw new FunctionVisitException(AppErrorCodes.FUNC_IS_NOT_PUSH_FUNC);
    }
    else if (FromThirdField(field.Expression))
    {
        throw new FunctionVisitException(AppErrorCodes.FUNC_IS_NOT_PUSH_FUNC);
    }
}
```

见 [DataDeriveCompileContext.cs](../../../SchemaNode.App/Runtime/Compile/CompileContext/DataDeriveCompileContext.cs#L149-L200)。

这段校验的意义是：

- 结果的主键必须可以由输入参数或常量确定；
- 主键不能来自第三方字段，否则无法反推“哪个源记录变了”；
- 非主键字段如果来自第三方字段，同样禁止（否则无法保证增量更新）。

这正是演示中提到的：**场馆 ID 的获取使用特定语义函数，键在调用中是明确的，因此系统能登记血缘。**

### 4.4 运行期：观察者与推送

加载字段时，`DataDerive` 被解析成观察者链：

```csharp
PushFunc = funcNode;
AppFieldType? pushSource = Application.GetField(derive.Source);
pushSource.AddObserver(this);
PushSource = pushSource;

DataDeriveCompileContext compileContext = new DataDeriveCompileContext(context, funcNode);
FunctionTypeSchema pushSchema = await compileContext.VisitFunctionType();
if (compileContext.ThirdFields.Length > 0)
{
    ThirdPushFields = compileContext.ThirdFields;
    foreach (DataDeriveThirdFieldInfo push in compileContext.ThirdFields)
        Application.GetField(push.Field)?.AddObserver(this);
}
PushFuncSchema = pushSchema;
```

见 [AppFieldType.cs](../../../SchemaNode.App/Runtime/App/AppFieldType.cs#L260-L310)。

形成的依赖图是：

```text
源字段 ──AddObserver──→ 派生字段
第三方字段 ──AddObserver──→ 派生字段
```

注意：观察者关系是**反向**的。字段本身不知道自己依赖谁，只有它知道“谁在观察我”。推送时从变更字段出发，沿 `Observers` 向外扩散。

---

## 5. 增量推送

数据变更不会立刻重算全部派生字段。提交事务时，`ProcessDataPush` 按依赖层级逐步计算。

### 5.1 生成推送层级

```csharp
List<AppFieldType> baseFields = changeData.Changes.Keys.Where(p => p.HasObserver).ToList();

FieldDataPushLevel? root = null;
FieldDataPushLevel? curr = null;
Dictionary<AppFieldType, FieldDataPushLevel> updateFieldsLvlMap = new();

while (baseFields.Count > 0)
{
    FieldDataPushLevel next = new();

    foreach (AppFieldType node in baseFields.Where(p => p.HasObserver)
                 .SelectMany(p => p.Observers!).Distinct()
                 .Where(n => n is { EnableDynamicTable: true, IsForeignView: false }))
    {
        if (!updateFieldsLvlMap.TryGetValue(node, out FieldDataPushLevel? value))
        {
            next.Fields.Add(node);
            updateFieldsLvlMap.Add(node, next);
        }
        else
        {
            next.Fields.Add(node);
            value.Fields.Remove(node);
            updateFieldsLvlMap[node] = next;
        }
    }

    if (next.Fields.Count <= 0) break;
    ...
    baseFields = next.Fields.Where(p => p.HasObserver).ToList();
}
```

见 [AppDataTransactionExtension.cs](../../../SchemaNode.App/Data/AppDataTransactionExtension.cs#L350-L422)。

几个细节：

- 只处理 `HasObserver` 的字段；
- 只处理真正落库、且不是视图的字段；
- 一个字段如果被多个上游依赖，会被移动到**更低**的层级，保证所有上游先算完；
- 层级通过 `FieldDataPushLevel.Next` 串起来。

### 5.2 收集变更

每个派生字段按主键收集三类数据：

```text
Origins   变更前
Updates   变更后
UnChanged 未变但被引用
```

第三方字段也走同样流程：

```csharp
foreach (DataDeriveThirdFieldInfo thirdInfo in field.ThirdPushFields)
{
    if (thirdInfo.PushKeys.Count == 0)
    {
        context.LogWarning($"The third push field {thirdInfo.Field} ... has no push keys, skip ...");
        continue;
    }

    AppFieldType? thirdAppField = field.Application.GetField(thirdInfo.Field);
    if (thirdAppField == null) continue;
    GatherFieldChangeInfos(thirdAppField, thirdInfo);
}
```

见 [AppDataTransactionExtension.cs](../../../SchemaNode.App/Data/AppDataTransactionExtension.cs#L433-L466)。

`PushKeys` 是编译期记录的主键字段。没有它，系统无法判断第三方字段的哪一行发生了变化，只能跳过。

### 5.3 计算新旧结果

```csharp
List<IValueAccess?[]> originsPush = InitPushData(pushSourceChangeInfo.Origins.Values, pushSourceChangeInfo.UnChanged.Values);
List<IValueAccess?[]> updatesPush = InitPushData(pushSourceChangeInfo.Updates.Values, pushSourceChangeInfo.UnChanged.Values);

await FillPushData(originsPush, true);
await FillPushData(updatesPush);

ArrayNode oldResult = await CalcPushData(originsPush);
ArrayNode newResult = await CalcPushData(updatesPush);

await context.SaveIncrementalData(field, newResult, oldResult);
```

见 [AppDataTransactionExtension.cs](../../../SchemaNode.App/Data/AppDataTransactionExtension.cs#L586-L617)。

这里为什么要同时算“旧结果”和“新结果”？

因为汇总结果可能**迁移**：

```text
修改 Ann 的所属场馆
  → 旧场馆的计数 -1
  → 新场馆的计数 +1
```

只算新结果无法知道旧场馆需要减一。因此必须成对计算，再让 `SaveIncrementalData` 做差量写入。

这与演示中的观察一致：修改 Ann 的场馆后，“组织人汇总”不变，但“场馆汇总”发生了转移。

### 5.4 第三方字段的数据加载

如果引用的第三方数据不在本次变更集合中，需要在计算前补查：

```csharp
ArrayNode? fetchData = await context.GetSchemaDataAsync(thirdAppField.App, fromField.Name,
    target, AppSchemaDataResult.List, filter) as ArrayNode;
if (fetchData == null) continue;

DynamicTableSchema schema = fromField.GetDynamicTableSchema(context);
foreach (IValueAccess node in fetchData)
{
    if (node is not StructNode structNode) continue;
    string? key = schema.GetPrimaryKey(structNode);
    if (string.IsNullOrEmpty(key)) continue;
    if (tarChangeInfo.Origins.ContainsKey(key) || tarChangeInfo.Updates.ContainsKey(key)) continue;
    tarChangeInfo.UnChanged[key] = structNode;
}
```

见 [AppDataTransactionExtension.cs](../../../SchemaNode.App/Data/AppDataTransactionExtension.cs#L565-L583)。

如果复合主键可能产生过多组合，会退化为多个 `IN` 条件，避免生成超长 `OR`：

```csharp
if (filter == null || existedKeys.Count > MAX_COMBINE_CASE_COUNT)
{
    filter = null;
    foreach (DataPushPrimaryFieldAccess map in item)
    {
        ArrayNode? keysNode = CombineField(changeInfos, map.Key);
        ...
        kFilter = new AppSchemaDataFilterBinary(LogicType.Contains,
            new AppSchemaDataFilterValue(keysNode),
            new AppSchemaDataFilterField(map.DataField));
        filter = filter != null ? new AppSchemaDataFilterBinary(LogicType.AndAlso, filter, kFilter) : kFilter;
    }
}
```

见 [AppDataTransactionExtension.cs](../../../SchemaNode.App/Data/AppDataTransactionExtension.cs#L546-L560)。

---

## 6. 三种机制的执行位置

```text
           View                 Foreigns              DataDerive
定义       FieldView            Foreign[]             Derive
加载       校验结构             校验目标应用          编译函数、登记观察者
存储       无表，复用源表       外键列                独立结果表
查询       走源表               Join/校验             普通表查询
写入       禁止                普通写入              仅推送写入
变更       —                   —                     观察者链 + 新旧成对计算
```

在 tryit 的演示里：

- `users`、`venues` 是视图字段 → `View`；
- 参会人的 `orgnizer` 选择 → `Foreigns`；
- `组织人已参加汇总` → `DataDerive` + `Combine.Count`；
- `场馆参会汇总` → `DataDerive` + 第三方字段 `getfield` + 血缘追踪。

---

## 7. 小结

派生层的核心不是“怎么算”，而是“**怎么知道该重算**”。

```text
DataDerive
  ├── 编译期：解析函数，记录源字段 / 第三方字段 / 主键映射
  ├── 加载期：建立观察者链
  ├── 事务提交：从变更字段出发，按依赖层级扩散
  └── 计算：新旧结果成对计算，差量写入
```

View 与 Foreigns 则是更轻的机制：一个复用源表，一个只是引用目标。三者共同让字段之间形成可追踪的语义网络，而不是散落在业务代码里的手写同步逻辑。

下一章进入工作流：节点图如何表达流程，上下文如何保存状态，事件如何触发起始节点。
