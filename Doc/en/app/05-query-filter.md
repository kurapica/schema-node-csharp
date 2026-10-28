# 05. Query and Filtering

In the data contract of the previous chapter, the query condition has only one type: `AppSchemaDataFilter`. This chapter explains how this filter is constructed, how it is simplified, how it is merged with row-level permissions, and finally how it is handed to the Provider for execution.

---

## 1. Inputs and Outputs of a Query

### 1.1 Result Forms

[AppSchemaDataFilter.cs](../../../SchemaNode.App/Data/AppSchemaDataFilter.cs#L23-L31) defines six query results:

```csharp
public enum AppSchemaDataResult
{
    List,    // List
    Count,   // Count
    Exist,   // Whether it exists
    First,   // First record
    Last,    // Last record
    Field,   // Value set of a field
}
```

The same method is reused through `AppSchemaDataResult`, rather than providing a separate API for each form. The Provider also only needs to implement the query semantics once.

### 1.2 Filter AST

The filter is a minimal tree of record types:

```csharp
public abstract record AppSchemaDataFilter;
public sealed record AppSchemaDataFilterField(string Field): AppSchemaDataFilter;
public sealed record AppSchemaDataFilterUnary(LogicType Type, AppSchemaDataFilter Operand): AppSchemaDataFilter;
public sealed record AppSchemaDataFilterBinary(LogicType Type, AppSchemaDataFilter Left, AppSchemaDataFilter Right): AppSchemaDataFilter;
public sealed record AppSchemaDataFilterArith(ArithmeticType Type, AppSchemaDataFilter Left, AppSchemaDataFilter Right): AppSchemaDataFilter;
public sealed record AppSchemaDataFilterValue(object Value): AppSchemaDataFilter;
```

It expresses only four things:

```text
field reference     Field("orgnizer")
constant            Value(userId)
unary operation     IsNull / NotNull / Not
binary operation    = / <> / > / LIKE / IN / AND / OR
```

Sorting uses a separate `AppSchemaDataOrder`:

```csharp
public sealed record AppSchemaDataOrder(string Field, bool Desc);
```

There are no concepts such as "table", "join", or "subquery". Join information lives in `DynamicTableSchema.Joins`, and the Provider fills it in during translation. This keeps the filter a pure logical expression that can be reused by different execution layers.

---

## 2. Three Sources of Filters

### 2.1 Frontend JSON

The frontend passes in a friendlier JSON structure, which `ToAppSchemaDataFilterAsync` translates into the AST; see [AppSchemaDataFilter.cs](../../../SchemaNode.App/Data/AppSchemaDataFilter.cs#L227-L290):

```csharp
internal static async Task<AppSchemaDataFilter?> ToAppSchemaDataFilterAsync(
    this JsonObject filter, SchemaContext context, StructType structType, FieldFilter[]? fieldFilters = null)
```

Rules:

| JSON value | Generated filter |
| --- | --- |
| `"name": "Ann"` | `name = 'Ann'` |
| `"name": ["Ann", "Bob"]` | `name IN ('Ann', 'Bob')` |
| `"funcName": [args]` | Call a function to generate the filter (`FieldFilterMode.Filter`) |

The field filter mode is determined by the `Filters` property on the field:

```text
Exactly   → =
Prefix    → StartsWith
Suffix    → EndsWith
Contains  → Match
Filter    → call the configured semantic function
```

Only scalar, enum, and locale string fields can directly participate in filtering, avoiding implicit conversion of complex structures into strings.

### 2.2 C# Expressions

`AppSchemaDataFilterVisitor` translates LINQ expression trees into the AST; see [AppSchemaDataFilterVisitor.cs](../../../SchemaNode.App/Data/AppSchemaDataFilterVisitor.cs#L32-L45):

```csharp
AppSchemaDataFilter filter = AppSchemaDataFilterVisitor.Build(cond);
```

It supports:

- single-parameter lambdas;
- comparison, boolean, and arithmetic operations;
- `string.Contains / StartsWith / EndsWith`;
- `Enumerable.Contains`;
- constants and member access (member access is evaluated into a constant).

Unsupported methods throw `NotSupportedException` directly, rather than silently generating incorrect SQL.

### 2.3 Semantic Functions

Row-level permission filters come from function calls:

```csharp
AppSchemaDataFilter? f = await policy.FilterFunc.CallAsync<AppSchemaDataFilter, QueryFilterCompileContext>(context, []);
```

The filter returned by the function is merged with the frontend filter; see the next section.

---

## 3. Simplification

Before the AST is handed to the Provider, it must go through `Transform`; see [AppSchemaDataFilter.cs](../../../SchemaNode.App/Data/AppSchemaDataFilter.cs#L66-L226).

It performs three kinds of simplification:

```text
1. Constant folding
   true AND x  → x
   false AND x → false
   true OR x   → true
   false OR x  → x

2. Null folding
   field = null → false (suggest the caller use IsNull)
   empty string / empty array in a comparison → false

3. Type constraints
   the operand of IsNull / NotNull must be a field
   unsupported operations directly return false
```

A filter that fails simplification is not treated as an "unconditional query" but as "no results":

```csharp
if (isValidFilter && filter is AppSchemaDataFilterValue or AppSchemaDataFilterField)
    isValidFilter = false;
```

See [BatchQueryAppData.cs](../../../SchemaNode.App/Api/Application/BatchQueryAppData.cs#L177-L186).

> This is a safe default: when the filter is invalid, return an empty result instead of leaking all data.

---

## 4. Query Execution Chain

The main flow of a batch query is in [BatchQueryAppData.cs](../../../SchemaNode.App/Api/Application/BatchQueryAppData.cs#L100-L265):

```text
iterate fields
  ├── compute DataRead / DataCreate / DataUpdate / DataDelete
  ├── SchemaOnly or no read permission → skip data
  ├── compute take
  │     ├── Pageable → IncrFieldDefaultTakeCount / IncrFieldMaxTakeCount
  │     └── non-Pageable → 0 (all)
  ├── row-level permission
  │     ├── evaluate Evaluator for each RowAuths entry
  │     ├── call FilterFunc to obtain the filter
  │     └── merge with the frontend filter via AND
  ├── Transform simplification
  ├── GetAppFieldDataAsync
  │     ├── PrepareFieldDataAsync
  │     ├── provider.QueryDynamicTableAsync
  │     └── GenerateDisplayOnlyFields
  ├── column-level permission
  │     └── remove unauthorized columns from the result
  └── enum access scan
```

### 4.1 Merging Row-Level Permissions

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

This code has two semantic points:

1. **Check the evaluator first, then the filter**: a false `Evaluator` means the policy does not apply; when true, `FilterFunc` determines the row range;
2. **Use only the first effective policy**: once a filter is obtained, `break`, avoiding conflicts among multiple policies.

The filter type must match the field's element structure; otherwise the whole query is marked invalid.

### 4.2 Paging

Paging is bound to the field's `Pageable` property:

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

- Fields with `Pageable = true` are incremental push fields and must have an upper bound;
- Non-pageable fields must not be truncated, so `take` is forced to 0.

The upper bound comes from configuration, preventing clients from requesting excessively large pages.

### 4.3 Column-Level Permissions

After the query completes, `ColPolicies` are evaluated column by column, and those that do not pass are removed from the result:

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

Removal happens **after the Provider returns**, because column permissions may depend on complex functions and cannot simply be pushed down as SQL column pruning. For large data volumes, this is a trade-off: less query complexity in exchange for complete permission semantics.

### 4.4 Filling in Display Fields

`DisplayOnly` fields are not stored in the table. After the query finishes, they are filled in uniformly by `GenerateDisplayOnlyFields`:

```csharp
if (genDisplayOnly)
    await schema.GenerateDisplayOnlyFields(context, result);
```

See [AppDataQueryExtension.cs](../../../SchemaNode.App/Data/AppDataQueryExtension.cs#L31-L35).

There are two ways to fill them in:

- The Provider does it at the SQL layer through `Joins`;
- The in-memory Provider evaluates relation functions on the result.

Therefore, "cross-application view fields" appear as ordinary fields to callers, but the storage layer does not copy data.

---

## 5. Queries Inside Semantic Functions

Scenarios such as data derivation and aggregation do not go through the HTTP layer but call `GetSchemaDataAsync` directly; see [AppDataQueryExtension.cs](../../../SchemaNode.App/Data/AppDataQueryExtension.cs#L142-L178):

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

This code additionally handles the target:

- When no target is passed explicitly, take the current target from the `Access` context;
- A business-target application must have a target, otherwise return empty;
- `StackAccess(app, target)` pushes the application and target onto the access stack, so subsequent Entity, view, and derived queries inherit the same isolation scope.

---

## 6. How the Provider Interprets Filters

The Provider only receives the AST and `DynamicTableSchema`, and must do the translation itself.

### 6.1 In-Memory Implementation

`InMemoryAppDataProvider` directly calls `filter.Test(context, node)`:

```csharp
bool matched = filter?.Test(schemaContext, node) ?? true;
```

Sorting, skipping, and truncation are all done in memory. It does not optimize, but it is semantically complete.

### 6.2 SQL Implementation

The SQL Provider uses `AppSchemaDataFilterVisitor` to do a single "AST → SQL fragment" traversal, and `ISqlProvider` handles dialect differences:

```text
AppSchemaDataFilterBinary
  ├── Equal         → Binary(Equal, left, right)
  ├── StartsWith    → LikeStartsWith(...)
  ├── Contains      → LikeContains(...)
  ├── In            → In(field, params)
  └── AndAlso       → Binary(AndAlso, ...)
```

Parameter names, quoting, and literals are all generated by `ISqlProvider`:

```csharp
public string QuoteField(string fieldName) => fieldName;
public string GenParameterName(int index) => $"@p{index}";
public string Literal(object? value) => value switch { ... };
```

See [DefaultSqlProvider.cs](../../../SchemaNode.App/Data/Sql/DefaultSqlProvider.cs#L8-L41).

> The filter does not contain information about "which table", so the SQL Provider must first determine the main table, joins, and column name mappings from `DynamicTableSchema`, and then translate the conditions.

---

## 7. Summary

```text
frontend JSON ─┐
C# expressions ─┼→ AppSchemaDataFilter ─→ Transform ─→ Provider
semantic function ─┘                          ↑
                                     RowAuths filter

result
  ├── List / Count / Exist / First / Last / Field
  ├── Pageable throttling
  ├── DisplayOnly filling
  └── ColAuths column pruning
```

The design principles of the query layer can be summarized as:

1. **Conditions decoupled from storage**: a unified AST that does not expose SQL;
2. **Permissions merged at the query entry**: row permissions and business conditions are merged before entering the Provider;
3. **Invalid means empty**: when the filter is invalid, return an empty result to avoid privilege escalation;
4. **Views and derived fields are transparent to queries**: callers see ordinary fields, while the complexity falls on the table structure and the Provider.

The next chapter discusses permissions themselves: evaluators, inheritance rules, and the four PolicyScope categories Schema / Data / Row / Col.
