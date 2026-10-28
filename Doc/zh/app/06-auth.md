# 06. 权限

App 方言把权限拆成三种粒度：

```text
Auths     → 能不能做（Schema / Data / Func）
RowAuths  → 能操作哪些行
ColAuths  → 能看哪些列
```

三者的实现方式不同，但都遵循同一条原则：**权限是语义配置，执行层只负责评估与落实。**

---

## 1. 权限的评估单元

### 1.1 PolicyScope

[Auths.cs](../../../SchemaNode.App/Property/App/Auths.cs#L61-L108) 定义了九种作用域：

```csharp
public enum PolicyScope
{
    SchemaCreate = 1,
    SchemaRead,
    SchemaUpdate,
    SchemaDelete,
    DataCreate,
    DataRead,
    DataUpdate,
    DataDelete,
    FuncExecute,
}
```

前四种约束“定义能否被修改”，中间四种约束“数据能否被读写”，最后一种约束“函数能否被执行”。

### 1.2 PolicyItem

一条策略由作用域和评估器组成：

```csharp
public sealed class PolicyItem
{
    public required PolicyScope Scope { get; set; }

    [Meta<SchemaType>(typeof(EvaluatorType))]
    public required string Evaluator { get; set; }

    [SchemaIgnore]
    [JsonIgnore]
    public FunctionType? Function { get; set; }
}
```

[PolicyItem.cs](../../../SchemaNode.App/Property/App/Auths.cs#L114-L133) 与 `EvaluatorType` 共同构成策略单元：

```csharp
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP}.policy.evaluator")]
[Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_FUNC_WITH_ARGS, NODE_SELF)]
public class EvaluatorType : ValidFuncType;
```

`LoadAsync` 会校验两件事，见 [Auths.cs](../../../SchemaNode.App/Property/App/Auths.cs#L32-L41)：

```csharp
item.Function = await context.GetNodeTypeAsync<FunctionType>(item.Evaluator);
if (item.Function is { Args.Length: 0 } && item.Function.Return.IsAssignableTo(context.System.Bool)) continue;
Error ??= AppErrorCodes.APP_POLICY_EVALUATOR_NOT_VALID;
```

- 评估器必须是**无参数**函数；
- 返回值必须可赋值给 `bool`。

参数不是通过 API 传入的，而是评估器内部从请求上下文取：

```js
(userid) => ...
```

实际上是 `system.data.getcontext('user.id')` 之类，在函数章节展开。

---

## 2. 继承式权限

策略的查找顺序是“先父级，后自身”。任何一个评估器返回 `true` 就通过。

### 2.1 类型

```csharp
private static IEnumerable<PolicyItem> GetAuthPolicies(this NodeType type, SchemaContext context, PolicyScope scope)
{
    NamespaceType? parent = type.Name.Equals(NS_SYSTEM, StringComparison.OrdinalIgnoreCase)
        ? null
        : type.Namespace ?? context.System.Self;

    if (parent != null)
        foreach (PolicyItem item in parent.GetAuthPolicies(context, scope))
            yield return item;

    PolicyItem[]? auths = type.GetProperty<Auths>()?.Value;
    ...
}
```

见 [Auths.cs](../../../SchemaNode.App/Property/App/Auths.cs#L174-L194)。

查找链是：

```text
Type
  → Namespace
      → 父命名空间
          → ...
              → system.Self
```

### 2.2 应用

```text
AppType
  → Container（父应用）
      → ...
          → system 应用
```

见 [Auths.cs](../../../SchemaNode.App/Property/App/Auths.cs#L196-L214)。

### 2.3 应用字段与工作流

```text
AppFieldType    → Application → Container → system
AppWorkflowType → Application → Container → system
```

见 [Auths.cs](../../../SchemaNode.App/Property/App/Auths.cs#L216-L241)。

### 2.4 为什么用 system 作为顶级

在演示中，管理员权限配置在 `system` 上，原因就在这里：

- `system` 的上级不可访问，因此它是继承链的终点；
- 如果用户在 `system` 上有某权限，则所有子类型、子应用、子字段都继承该权限；
- 不需要为每个类型分别配置管理员。

这与 tryit 演示中的描述一致：**在父级具有权限，即具有所有子级的对应权限。**

---

## 3. 评估执行

所有评估最终收敛到两个方法，见 [Auths.cs](../../../SchemaNode.App/Property/App/Auths.cs#L248-L318)。

### 3.1 按策略集合评估

```csharp
private async Task<bool> AuthorizeAsync(IEnumerable<PolicyItem> items, bool silent = false)
{
    bool authorized = true;
    PolicyEvaluatorResult cache = context.GetOrAddContextItem<PolicyEvaluatorResult>();

    foreach (PolicyItem item in items)
    {
        try
        {
            if (!cache.Result.TryGetValue(item.Evaluator, out authorized))
            {
                bool? result = await context.CallFunctionAsync<bool>(item.Evaluator, []);
                if (result.HasValue)
                {
                    authorized = result.Value;
                    cache.Result[item.Evaluator] = authorized;
                }
            }

            if (authorized)
                break;
        }
        catch (Exception ex)
        {
            context.LogError(ex, "Policy evaluation error for {policy}", item.Evaluator);
        }
    }

    if (!silent && !authorized) throw new UnauthorizedAccessException();
    return authorized;
}
```

四个行为值得注意：

| 行为 | 原因 |
| --- | --- |
| 无策略时 `authorized = true` | 默认开放，权限是显式加强 |
| 结果按评估器名缓存 | 同一次请求内同一函数只执行一次 |
| 任一为 `true` 即 `break` | 继承链上“父级有权限”即通过 |
| 异常被记录但不中断 | 评估失败按 `false` 处理，避免误放行 |

`silent` 参数区分两种调用方式：

- `silent = true`：只返回结果，用于前端展示按钮状态；
- `silent = false`：无权限直接抛 `UnauthorizedAccessException`，用于真正执行。

### 3.2 直接按评估器评估

行权限、列权限不走 PolicyItem 集合，而是直接调用函数：

```csharp
public async Task<bool> AuthorizeAsync(string evaluator, bool silent = false)
{
    PolicyEvaluatorResult cache = context.GetOrAddContextItem<PolicyEvaluatorResult>();

    if (!cache.Result.TryGetValue(evaluator, out var authorized))
    {
        try
        {
            bool? result = await context.CallFunctionAsync<bool>(evaluator, []);
            if (result.HasValue)
            {
                authorized = result.Value;
                cache.Result[evaluator] = authorized;
            }
        }
        catch (Exception ex)
        {
            context.LogError(ex, "Policy evaluation error for {evaluator}", evaluator);
        }
    }

    if (!silent && !authorized) throw new UnauthorizedAccessException();
    return authorized;
}
```

它与集合版本共享同一份缓存，因此“同一个函数在一次请求中只计算一次”的约定对所有权限类型都成立。

---

## 4. Schema 投影中的权限

权限不仅影响执行，也影响前端看到的 Schema。`GetSchemaAsync` 会把评估结果写回属性：

```csharp
schema.SetProperty<SchemaCreate, bool>(!isSystem && await context.AuthorizeAsync(this, PolicyScope.SchemaCreate, true));
schema.SetProperty<SchemaRead, bool>(await context.AuthorizeAsync(this, PolicyScope.SchemaRead, true));
schema.SetProperty<SchemaUpdate, bool>(await context.AuthorizeAsync(this, PolicyScope.SchemaUpdate, true));
schema.SetProperty<SchemaDelete, bool>(!isSystem && await context.AuthorizeAsync(this, PolicyScope.SchemaDelete, true));
```

见 [AppType.cs](../../../SchemaNode.App/Runtime/App/AppType.cs#L315-L350) 与 [AppFieldType.cs](../../../SchemaNode.App/Runtime/App/AppFieldType.cs#L421-L472)。

字段还多两个约束：

```csharp
!isSystem && !ReadOnly && await context.AuthorizeAsync(this, PolicyScope.DataCreate, true)
```

- 系统字段不允许普通用户创建；
- `ReadOnly` 字段（例如视图、派生字段）不允许写。

前端据此禁用按钮，但正如 tryit 中强调的：**前端状态只是可视化，真正的访问控制仍在后端。**

---

## 5. 行级权限

### 5.1 结构

[RowAuths.cs](../../../SchemaNode.App/Property/App/RowAuths.cs#L62-L91)：

```csharp
public sealed class RowPolicy
{
    [Meta<SchemaType>(typeof(EvaluatorType))]
    public required string Evaluator { get; set; }

    [Meta<SchemaType>(typeof(ValidFuncType))]
    [Relation<Valid, Relation.Assign>(nameof(RowPolicy.Filter), ...)]
    public required string Filter { get; set; }

    [Meta<SchemaType>(typeof(Schema.ValueType))]
    [Meta<DisplayOnly>(true)]
    [Meta<InVisible>(true)]
    public string? FieldType { get; set; }
}
```

语义是：

```text
Evaluator 为 true  → 使用 Filter 限制行
Evaluator 为 false → 该策略不适用
Filter 为空       → 不做行限制（但 Evaluate 仍需为 true）
```

### 5.2 类型校验

`RowAuths.LoadAsync` 会检查过滤器函数：

```csharp
if (item.FilterFunc is { Args.Length: 1 } &&
    item.FilterFunc.Return.IsAssignableTo(context.System.Bool) &&
    (ownerType == null || item.FilterFunc.Args[0].ValueType!.IsAssignableTo(ownerType)))
    continue;
Error ??= AppErrorCodes.APP_ROW_AUTH_FILTER_NOT_VALID;
```

见 [RowAuths.cs](../../../SchemaNode.App/Property/App/RowAuths.cs#L28-L46)。

- 过滤器必须恰好一个参数；
- 参数类型必须能接收字段的元素类型；
- 返回 `bool`。

### 5.3 读：合并成查询条件

查询时，`RowPolicy.FilterFunc` 被编译成 `AppSchemaDataFilter`：

```csharp
AppSchemaDataFilter? f = await policy.FilterFunc.CallAsync<AppSchemaDataFilter, QueryFilterCompileContext>(context, []);
filter = filter == null ? f : filter.AndAlso(f);
```

见 [BatchQueryAppData.cs](../../../SchemaNode.App/Api/Application/BatchQueryAppData.cs#L133-L175)。

这与 tryit 中 `attendee.orgnizer = system.data.getcontext('user.id')` 的例子对应：函数体在编译期被翻译成过滤器，而不是逐行调用。

### 5.4 写：逐条校验

保存与删除时，过滤器退化为“单行校验函数”：

```csharp
static async Task ValidateRow(SchemaContext context, FunctionType rowChecker, JsonNode? item)
{
    if (item is not JsonObject) throw new UnauthorizedAccessException();
    try
    {
        var args = new JsonArray(1) { [0] = item.DeepClone() };
        var res = await context.CallFunctionAsync(rowChecker, args, NS_SYSTEM_BOOL);
        if (res is not JsonValue boolVal || !boolVal.TryGetValue(out bool allowed) || !allowed)
            throw new UnauthorizedAccessException();
    }
    catch
    {
        throw new UnauthorizedAccessException();
    }
}
```

见 [PushAppData.cs](../../../SchemaNode.App/Api/Application/PushAppData.cs#L183-L200)。

任何异常、空结果、非布尔结果都视为拒绝。这是行权限的“失败即拒绝”原则。

> 注意读和写使用同一份 `Filter`，但执行方式不同：读是下推为条件，写是逐行求值。两者语义一致，性能特性不同。

---

## 6. 列级权限

[ColAuths.cs](../../../SchemaNode.App/Property/App/ColAuths.cs#L73-L97)：

```csharp
public sealed class ColPolicy
{
    public required string Name { get; set; }
    public string[] Evaluators { get; set; } = [];
}
```

- 按列名配置；
- 多个评估器，任一为 `true` 即允许；
- 列必须存在于字段的结构体中，否则 `LoadAsync` 报 `APP_COL_AUTH_FIELD_NOT_FOUND`。

查询结果返回后，未授权列会被删除：

```csharp
foreach (StructFieldType f in @struct.GetFields())
{
    bool authorized = true;
    foreach (string evaluator in field.GetColPolicies(f.Name))
    {
        authorized = await context.AuthorizeAsync(evaluator, true);
        if (authorized) break;
    }
    if (authorized) continue;
    ignoreFields.Add(f.Name);
}
```

见 [BatchQueryAppData.cs](../../../SchemaNode.App/Api/Application/BatchQueryAppData.cs#L212-L259)。

`GetColPolicies` 本身是一个简单的扩展方法：

```csharp
public static IEnumerable<string> GetColPolicies(this AppFieldType appFieldType, string fieldName)
{
    ColPolicy[]? colPolicies = appFieldType.GetProperty<ColAuths>()?.Value;
    ColPolicy? item = colPolicies?.FirstOrDefault(i => i.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
    if (item == null || item.Evaluators.Length == 0) yield break;
    foreach (var evaluator in item.Evaluators) yield return evaluator;
}
```

见 [ColAuths.cs](../../../SchemaNode.App/Property/App/ColAuths.cs#L99-L107)。

---

## 7. 作用域策略

`ScopePolicy` 不属于权限本身，但它决定了“数据属于谁”，间接决定了权限的边界。

[ScopePolicy.cs](../../../SchemaNode.App/Property/App/ScopePolicy.cs#L34-L57)：

```csharp
public sealed class AppScopePolicy
{
    [Meta<Default>(AppScopeType.BusinessTarget)]
    public AppScopeType Type { get; set; } = AppScopeType.BusinessTarget;

    [Relation<Visible, Call>(NODE_SELF, NS_SYSTEM_LOGIC_EQ, $"{nameof(Type)}", AppScopeType.IsolationContext)]
    public AppScopeContextMap[]? ContextMaps { get; set; }
}
```

[AppScopeType.cs](../../../SchemaNode.App/Enum/AppScopeType.cs#L11-L26)：

```csharp
public enum AppScopeType
{
    BusinessTarget = 0,   // 以业务对象为目标，默认
    SystemLevel = 1,      // 无目标，全局应用
    IsolationContext = 2, // 以上下文字段隔离，如 tenantId / orgId
}
```

三种模式对应三种数据边界：

```text
SystemLevel        → 全局共享（用户、组织等基础应用）
BusinessTarget     → 每个 target 一份数据（活动、项目）
IsolationContext   → 每个上下文组合一份数据（租户、组织隔离）
```

`ContextMaps` 仅在 `IsolationContext` 下可见，它把上下文字段映射到表中的作用域列。保存字段时会校验它不能与业务字段重名，见 [SchemaStorageProviderExtension.cs](../../../SchemaNode.App/Schema/Provider/SchemaStorageProviderExtension.cs#L293-L327)。

---

## 8. 小结

```text
Auths
  ├── PolicyScope（9 种）
  ├── 继承链（父级优先）
  ├── 评估器（无参 bool）
  └── 请求内缓存

RowAuths
  ├── Evaluator → 是否适用
  ├── Filter → 读：查询条件 / 写：逐行校验
  └── 失败即拒绝

ColAuths
  ├── 按列配置
  ├── 多评估器任一通过
  └── 结果裁剪

ScopePolicy
  ├── SystemLevel
  ├── BusinessTarget
  └── IsolationContext
```

权限层的关键不是“有几个检查点”，而是三件事：

1. **权限是声明式的**：评估器和过滤器都是语义函数；
2. **权限是继承的**：父级配置自动覆盖子级；
3. **权限在执行前合并**：读权限变成查询条件，写权限变成前置校验。

下一章进入派生层：视图、外键、汇总，以及数据变更时如何沿血缘找到需要重算的字段。
