# 05. 查询与过滤

上一章的数据契约里，查询条件只有一个类型：`AppSchemaDataFilter`。本章说明这个过滤器如何被构造、如何被化简、如何与行级权限合并，最后如何交给 Provider 执行。

---

## 1. 查询的输入与输出

### 1.1 结果形态

[AppSchemaDataFilter.cs](../../../SchemaNode.App/Data/AppSchemaDataFilter.cs#L23-L31) 定义了六种查询结果：

```csharp
public enum AppSchemaDataResult
{
    List,    // 列表
    Count,   // 计数
    Exist,   // 是否存在
    First,   // 第一条
    Last,    // 最后一条
    Field,   // 取某个字段的值集合
}
```

同一个方法通过 `AppSchemaDataResult` 复用，而不是为每种形态提供独立 API。Provider 也只需实现一次查询语义。

### 1.2 过滤器 AST

过滤器是一个极简的记录类型树：

```csharp
public abstract record AppSchemaDataFilter;
public sealed record AppSchemaDataFilterField(string Field): AppSchemaDataFilter;
public sealed record AppSchemaDataFilterUnary(LogicType Type, AppSchemaDataFilter Operand): AppSchemaDataFilter;
public sealed record AppSchemaDataFilterBinary(LogicType Type, AppSchemaDataFilter Left, AppSchemaDataFilter Right): AppSchemaDataFilter;
public sealed record AppSchemaDataFilterArith(ArithmeticType Type, AppSchemaDataFilter Left, AppSchemaDataFilter Right): AppSchemaDataFilter;
public sealed record AppSchemaDataFilterValue(object Value): AppSchemaDataFilter;
```

它只表达四件事：

```text
字段引用     Field("orgnizer")
常量         Value(userId)
一元运算     IsNull / NotNull / Not
二元运算     = / <> / > / LIKE / IN / AND / OR
```

排序则用独立的 `AppSchemaDataOrder`：

```csharp
public sealed record AppSchemaDataOrder(string Field, bool Desc);
```

没有“表”“连接”“子查询”这些概念。连接信息在 `DynamicTableSchema.Joins` 中，由 Provider 在翻译时补齐。这让过滤器保持为纯逻辑表达式，可以被不同执行层复用。

---

## 2. 过滤器的三种来源

### 2.1 前端 JSON

前端传入的是更友好的 JSON 结构，`ToAppSchemaDataFilterAsync` 把它翻译成 AST，见 [AppSchemaDataFilter.cs](../../../SchemaNode.App/Data/AppSchemaDataFilter.cs#L227-L290)：

```csharp
internal static async Task<AppSchemaDataFilter?> ToAppSchemaDataFilterAsync(
    this JsonObject filter, SchemaContext context, StructType structType, FieldFilter[]? fieldFilters = null)
```

规则：

| JSON 值 | 生成的过滤器 |
| --- | --- |
| `"name": "Ann"` | `name = 'Ann'` |
| `"name": ["Ann", "Bob"]` | `name IN ('Ann', 'Bob')` |
| `"funcName": [args]` | 调用函数生成过滤器（`FieldFilterMode.Filter`） |

字段过滤模式由字段上的 `Filters` 属性决定：

```text
Exactly   → =
Prefix    → StartsWith
Suffix    → EndsWith
Contains  → Match
Filter    → 调用配置的语义函数
```

只有标量、枚举和 locale 字符串字段能直接参与过滤，避免把复杂结构隐式转成字符串。

### 2.2 C# 表达式

`AppSchemaDataFilterVisitor` 把 LINQ 表达式树翻译成 AST，见 [AppSchemaDataFilterVisitor.cs](../../../SchemaNode.App/Data/AppSchemaDataFilterVisitor.cs#L32-L45)：

```csharp
AppSchemaDataFilter filter = AppSchemaDataFilterVisitor.Build(cond);
```

它支持：

- 单参数 lambda；
- 比较运算、布尔运算、算术运算；
- `string.Contains / StartsWith / EndsWith`；
- `Enumerable.Contains`；
- 常量与成员访问（成员访问会被求值成常量）。

不支持的方法会直接抛 `NotSupportedException`，而不是静默生成错误 SQL。

### 2.3 语义函数

行级权限的过滤器来自函数调用：

```csharp
AppSchemaDataFilter? f = await policy.FilterFunc.CallAsync<AppSchemaDataFilter, QueryFilterCompileContext>(context, []);
```

函数返回的过滤器与前端过滤器合并，见下一节。

---

## 3. 化简

AST 在交给 Provider 前必须经过 `Transform`，见 [AppSchemaDataFilter.cs](../../../SchemaNode.App/Data/AppSchemaDataFilter.cs#L66-L226)。

它做三类简化：

```text
1. 常量折叠
   true AND x  → x
   false AND x → false
   true OR x   → true
   false OR x  → x

2. 空值折叠
   field = null → false（提示调用方改用 IsNull）
   空字符串 / 空数组参与比较 → false

3. 类型约束
   IsNull / NotNull 的操作数必须是字段
   不支持的运算直接返回 false
```

化简失败的过滤器不会被当作“无条件查询”，而是被视为“无结果”：

```csharp
if (isValidFilter && filter is AppSchemaDataFilterValue or AppSchemaDataFilterField)
    isValidFilter = false;
```

见 [BatchQueryAppData.cs](../../../SchemaNode.App/Api/Application/BatchQueryAppData.cs#L177-L186)。

> 这是一个安全默认值：过滤器无效时返回空结果，而不是泄露全量数据。

---

## 4. 查询的执行链

一次批量查询的主流程在 [BatchQueryAppData.cs](../../../SchemaNode.App/Api/Application/BatchQueryAppData.cs#L100-L265)：

```text
遍历字段
  ├── 计算 DataRead / DataCreate / DataUpdate / DataDelete
  ├── SchemaOnly 或无权读取 → 跳过数据
  ├── 计算 take
  │     ├── Pageable → IncrFieldDefaultTakeCount / IncrFieldMaxTakeCount
  │     └── 非 Pageable → 0（全量）
  ├── 行级权限
  │     ├── RowAuths 逐条评估 Evaluator
  │     ├── 调用 FilterFunc 得到过滤器
  │     └── 与前端过滤器 AND 合并
  ├── Transform 化简
  ├── GetAppFieldDataAsync
  │     ├── PrepareFieldDataAsync
  │     ├── provider.QueryDynamicTableAsync
  │     └── GenerateDisplayOnlyFields
  ├── 列级权限
  │     └── 从结果中移除未授权列
  └── 枚举访问扫描
```

### 4.1 行级权限的合并

```csharp
if (field is { ValueType: ArrayType { Element: StructType structType } } &&
    field.GetProperty<RowAuths>() is { Value.Length: > 0 } rowAuths)
{
    foreach (RowPolicy policy in rowAuths.Value)
    {
        bool authorized = await context.AuthorizeAsync(policy.Evaluator, true);
        if (!authorized) continue;
        if (policy.FilterFunc == null) break;

        if (policy.FilterFunc.Args.Length != 1 ||
            !policy.FilterFunc.Args[0].ValueType!.IsAssignableTo(structType))
        {
            isValidFilter = false;
            break;
        }

        AppSchemaDataFilter? f = await policy.FilterFunc.CallAsync<AppSchemaDataFilter, QueryFilterCompileContext>(context, []);
        filter = filter == null ? f : filter.AndAlso(f);
        break;
    }
}

if (q?.Filter != null)
{
    var qFilter = await q.Filter.ToAppSchemaDataFilterAsync(context, structType, field.Filters);
    filter = filter != null && qFilter != null ? filter.AndAlso(qFilter) : (filter ?? qFilter);
}
```

这段代码有两个语义：

1. **先看评估器，再看过滤器**：`Evaluator` 为假表示该策略不适用；为真时，`FilterFunc` 决定行范围；
2. **只使用第一条生效策略**：一旦拿到过滤器就 `break`，避免多条策略互相冲突。

过滤器类型必须与字段元素结构匹配，否则整个查询被标记为无效。

### 4.2 分页

分页与字段的 `Pageable` 属性绑定：

```csharp
int take = q?.Take ?? query.Take ?? 0;
if (field.Pageable == true)
{
    take = take <= 0
        ? SchemaNodeConfig.Current.IncrFieldDefaultTakeCount
        : Math.Min(take, SchemaNodeConfig.Current.IncrFieldMaxTakeCount);
}
else
{
    take = 0;
}
```

- `Pageable = true` 的字段是增量推送字段，必须有上限；
- 非分页字段不允许截断，因此 `take` 强制为 0。

上限来自配置，避免客户端请求过大的页。

### 4.3 列级权限

查询完成后，逐列评估 `ColPolicies`，未通过的直接从结果中删除：

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
    ignoreFields ??= [];
    ignoreFields.Add(f.Name);
}
```

删除发生在 **Provider 返回之后**，因为列权限可能依赖复杂函数，不能简单下推成 SQL 列裁剪。对数据量大的场景，这是一种取舍：以更少的查询复杂度换取完整的权限语义。

### 4.4 显示字段的补齐

`DisplayOnly` 字段不在表中存储。查询结束后由 `GenerateDisplayOnlyFields` 统一补齐：

```csharp
if (genDisplayOnly)
    await schema.GenerateDisplayOnlyFields(context, result);
```

见 [AppDataQueryExtension.cs](../../../SchemaNode.App/Data/AppDataQueryExtension.cs#L31-L35)。

补齐方式有两种：

- Provider 通过 `Joins` 在 SQL 层完成；
- 内存 Provider 在结果上调用关系函数求值。

因此“跨应用视图字段”对调用方表现为普通字段，但存储层并不复制数据。

---

## 5. 语义函数中的查询

数据派生、汇总等场景不经过 HTTP 层，而是直接调用 `GetSchemaDataAsync`，见 [AppDataQueryExtension.cs](../../../SchemaNode.App/Data/AppDataQueryExtension.cs#L142-L178)：

```csharp
public static async Task<IValueAccess?> GetSchemaDataAsync(
    this SchemaContext context, string app, string field, string? target, AppSchemaDataResult type,
    AppSchemaDataFilter? filter = null, int skip = 0, int take = 0, bool desc = false,
    AppSchemaDataOrder[]? orderBy = null, string? dataField = null)
{
    AppType? appType = await context.GetAppTypeAsync(app);
    AppFieldType? appField = appType?.GetField(field);
    if (appField == null) return null;

    bool isValidFilter = filter == null;
    if (filter != null)
    {
        isValidFilter = filter.Transform(out AppSchemaDataFilter? final);
        filter = final;
        if (isValidFilter && filter is AppSchemaDataFilterValue or AppSchemaDataFilterField)
            isValidFilter = false;
    }

    if (string.IsNullOrEmpty(target))
        target = context.GetContextItem<Access>()?.Target ?? string.Empty;

    if (!isValidFilter || string.IsNullOrEmpty(target) && appType?.ScopeType != AppScopeType.SystemLevel)
        return empty result;

    using var stack = context.StackAccess(app, target);
    ...
}
```

这段代码额外处理了目标：

- 未显式传 target 时，从 `Access` 上下文取当前目标；
- 业务目标应用必须有 target，否则返回空；
- `StackAccess(app, target)` 把应用与目标压入访问栈，使后续的 Entity、视图、派生查询都继承同样的隔离范围。

---

## 6. Provider 如何解释过滤器

Provider 只拿到 AST 和 `DynamicTableSchema`，需要自己做翻译。

### 6.1 内存实现

`InMemoryAppDataProvider` 直接调用 `filter.Test(context, node)`：

```csharp
bool matched = filter?.Test(schemaContext, node) ?? true;
```

排序、跳过、截断都在内存中完成。它不优化，但语义完整。

### 6.2 SQL 实现

SQL Provider 用 `AppSchemaDataFilterVisitor` 做一次“AST → SQL 片段”的遍历，`ISqlProvider` 负责方言差异：

```text
AppSchemaDataFilterBinary
  ├── Equal         → Binary(Equal, left, right)
  ├── StartsWith    → LikeStartsWith(...)
  ├── Contains      → LikeContains(...)
  ├── In            → In(field, params)
  └── AndAlso       → Binary(AndAlso, ...)
```

参数名、引号、字面量都由 `ISqlProvider` 生成：

```csharp
public string QuoteField(string fieldName) => fieldName;
public string GenParameterName(int index) => $"@p{index}";
public string Literal(object? value) => value switch { ... };
```

见 [DefaultSqlProvider.cs](../../../SchemaNode.App/Data/Sql/DefaultSqlProvider.cs#L8-L41)。

> 过滤器不包含“哪个表”的信息，因此 SQL Provider 必须先按 `DynamicTableSchema` 决定主表、Join、列名映射，再翻译条件。

---

## 7. 小结

```text
前端 JSON ─┐
C# 表达式 ─┼→ AppSchemaDataFilter ─→ Transform ─→ Provider
语义函数 ─┘                          ↑
                              RowAuths 过滤器

结果
  ├── List / Count / Exist / First / Last / Field
  ├── Pageable 限流
  ├── DisplayOnly 补齐
  └── ColAuths 列裁剪
```

查询层的设计原则可以概括为：

1. **条件与存储解耦**：统一 AST，不暴露 SQL；
2. **权限在查询入口合并**：行权限与业务条件在进入 Provider 前就已经合并；
3. **无效即空**：过滤器不合法时返回空结果，避免越权；
4. **视图与派生字段对查询透明**：调用方看到的是普通字段，复杂度落在表结构与 Provider。

下一章讨论权限本身：评估器、继承规则，以及 Schema / Data / Row / Col 四类 PolicyScope。
