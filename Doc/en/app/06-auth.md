# 06. Permissions

The App dialect splits permissions into three granularities:

```text
Auths     → can it be done (Schema / Data / Func)
RowAuths  → which rows can be operated on
ColAuths  → which columns can be seen
```

The three are implemented differently, but all follow the same principle: **permissions are semantic configuration, and the execution layer is only responsible for evaluating and enforcing them.**

---

## 1. The Evaluation Unit of Permissions

### 1.1 PolicyScope

[Auths.cs](../../../SchemaNode.App/Property/App/Auths.cs#L61-L108) defines nine scopes:

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

The first four constrain "whether a definition can be modified", the middle four constrain "whether data can be read or written", and the last constrains "whether a function can be executed".

### 1.2 PolicyItem

A policy consists of a scope and an evaluator:

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

[PolicyItem.cs](../../../SchemaNode.App/Property/App/Auths.cs#L114-L133) and `EvaluatorType` together form the policy unit:

```csharp
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP}.policy.evaluator")]
[Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_FUNC_WITH_ARGS, NODE_SELF)]
public class EvaluatorType : ValidFuncType;
```

`LoadAsync` validates two things; see [Auths.cs](../../../SchemaNode.App/Property/App/Auths.cs#L32-L41):

```csharp
item.Function = await context.GetNodeTypeAsync<FunctionType>(item.Evaluator);
if (item.Function is { Args.Length: 0 } && item.Function.Return.IsAssignableTo(context.System.Bool)) continue;
Error ??= AppErrorCodes.APP_POLICY_EVALUATOR_NOT_VALID;
```

- The evaluator must be a **parameterless** function;
- Its return value must be assignable to `bool`.

Parameters are not passed in through the API; instead, the evaluator itself takes them from the request context:

```js
(userid) => ...
```

In practice this is something like `system.data.getcontext('user.id')`, which will be covered in the functions chapter.

---

## 2. Inherited Permissions

The lookup order for policies is "parent first, then self". If any evaluator returns `true`, access is granted.

### 2.1 Types

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

See [Auths.cs](../../../SchemaNode.App/Property/App/Auths.cs#L174-L194).

The lookup chain is:

```text
Type
  → Namespace
      → parent namespace
          → ...
              → system.Self
```

### 2.2 Applications

```text
AppType
  → Container (parent application)
      → ...
          → system application
```

See [Auths.cs](../../../SchemaNode.App/Property/App/Auths.cs#L196-L214).

### 2.3 Application Fields and Workflows

```text
AppFieldType    → Application → Container → system
AppWorkflowType → Application → Container → system
```

See [Auths.cs](../../../SchemaNode.App/Property/App/Auths.cs#L216-L241).

### 2.4 Why system Is the Top Level

In the demo, administrator permissions are configured on `system`, and this is why:

- The parent of `system` is not accessible, so it is the terminus of the inheritance chain;
- If a user has a permission on `system`, then all subtypes, sub-applications, and sub-fields inherit that permission;
- There is no need to configure an administrator for each type separately.

This is consistent with the description in the tryit demo: **having a permission at the parent level means having the corresponding permission at all child levels.**

---

## 3. Evaluation Execution

All evaluations ultimately converge on two methods; see [Auths.cs](../../../SchemaNode.App/Property/App/Auths.cs#L248-L318).

### 3.1 Evaluate by Policy Set

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

Four behaviors are worth noting:

| Behavior | Reason |
| --- | --- |
| `authorized = true` when there are no policies | Open by default; permissions are explicit reinforcement |
| Results cached by evaluator name | The same function executes only once within the same request |
| `break` as soon as any is `true` | On the inheritance chain, "the parent has the permission" means it passes |
| Exceptions are logged but do not interrupt | An evaluation failure is treated as `false`, avoiding accidental allowance |

The `silent` parameter distinguishes two invocation modes:

- `silent = true`: only return the result, used by the frontend to display button states;
- `silent = false`: throw `UnauthorizedAccessException` directly when there is no permission, used for actual execution.

### 3.2 Evaluate Directly by Evaluator

Row permissions and column permissions do not go through the PolicyItem set; they call the function directly:

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

It shares the same cache as the set version, so the convention that "the same function is computed only once per request" holds for all permission types.

---

## 4. Permissions in Schema Projection

Permissions affect not only execution but also the Schema that the frontend sees. `GetSchemaAsync` writes the evaluation results back into properties:

```csharp
schema.SetProperty<SchemaCreate, bool>(!isSystem && await context.AuthorizeAsync(this, PolicyScope.SchemaCreate, true));
schema.SetProperty<SchemaRead, bool>(await context.AuthorizeAsync(this, PolicyScope.SchemaRead, true));
schema.SetProperty<SchemaUpdate, bool>(await context.AuthorizeAsync(this, PolicyScope.SchemaUpdate, true));
schema.SetProperty<SchemaDelete, bool>(!isSystem && await context.AuthorizeAsync(this, PolicyScope.SchemaDelete, true));
```

See [AppType.cs](../../../SchemaNode.App/Runtime/App/AppType.cs#L315-L350) and [AppFieldType.cs](../../../SchemaNode.App/Runtime/App/AppFieldType.cs#L421-L472).

Fields have two additional constraints:

```csharp
!isSystem && !ReadOnly && await context.AuthorizeAsync(this, PolicyScope.DataCreate, true)
```

- System fields cannot be created by ordinary users;
- `ReadOnly` fields (such as views and derived fields) cannot be written.

The frontend disables buttons accordingly, but as emphasized in tryit: **the frontend state is only visualization; the real access control is still on the backend.**

---

## 5. Row-Level Permissions

### 5.1 Structure

[RowAuths.cs](../../../SchemaNode.App/Property/App/RowAuths.cs#L62-L91):

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

The semantics are:

```text
Evaluator is true  → use Filter to restrict rows
Evaluator is false → the policy does not apply
Filter is empty    → no row restriction (but Evaluate must still be true)
```

### 5.2 Type Validation

`RowAuths.LoadAsync` checks the filter function:

```csharp
if (item.FilterFunc is { Args.Length: 1 } &&
    item.FilterFunc.Return.IsAssignableTo(context.System.Bool) &&
    (ownerType == null || item.FilterFunc.Args[0].ValueType!.IsAssignableTo(ownerType)))
    continue;
Error ??= AppErrorCodes.APP_ROW_AUTH_FILTER_NOT_VALID;
```

See [RowAuths.cs](../../../SchemaNode.App/Property/App/RowAuths.cs#L28-L46).

- The filter must have exactly one parameter;
- The parameter type must be able to receive the field's element type;
- It returns `bool`.

### 5.3 Read: Merge into Query Conditions

During a query, `RowPolicy.FilterFunc` is compiled into an `AppSchemaDataFilter`:

```csharp
AppSchemaDataFilter? f = await policy.FilterFunc.CallAsync<AppSchemaDataFilter, QueryFilterCompileContext>(context, []);
filter = filter == null ? f : filter.AndAlso(f);
```

See [BatchQueryAppData.cs](../../../SchemaNode.App/Api/Application/BatchQueryAppData.cs#L133-L175).

This corresponds to the example `attendee.orgnizer = system.data.getcontext('user.id')` in tryit: the function body is translated into a filter at compile time rather than invoked row by row.

### 5.4 Write: Validate Row by Row

When saving and deleting, the filter degenerates into a "single-row validation function":

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

See [PushAppData.cs](../../../SchemaNode.App/Api/Application/PushAppData.cs#L183-L200).

Any exception, empty result, or non-boolean result is treated as a denial. This is the "fail closed" principle of row permissions.

> Note that reads and writes use the same `Filter`, but the execution differs: reads push it down as a condition, writes evaluate it row by row. Both have consistent semantics, with different performance characteristics.

---

## 6. Column-Level Permissions

[ColAuths.cs](../../../SchemaNode.App/Property/App/ColAuths.cs#L73-L97):

```csharp
public sealed class ColPolicy
{
    public required string Name { get; set; }
    public string[] Evaluators { get; set; } = [];
}
```

- Configured by column name;
- Multiple evaluators: any one that is `true` grants access;
- The column must exist in the field's struct; otherwise `LoadAsync` reports `APP_COL_AUTH_FIELD_NOT_FOUND`.

After the query result is returned, unauthorized columns are removed:

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

See [BatchQueryAppData.cs](../../../SchemaNode.App/Api/Application/BatchQueryAppData.cs#L212-L259).

`GetColPolicies` itself is a simple extension method:

```csharp
public static IEnumerable<string> GetColPolicies(this AppFieldType appFieldType, string fieldName)
{
    ColPolicy[]? colPolicies = appFieldType.GetProperty<ColAuths>()?.Value;
    ColPolicy? item = colPolicies?.FirstOrDefault(i => i.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
    if (item == null || item.Evaluators.Length == 0) yield break;
    foreach (var evaluator in item.Evaluators) yield return evaluator;
}
```

See [ColAuths.cs](../../../SchemaNode.App/Property/App/ColAuths.cs#L99-L107).

---

## 7. Scope Policies

`ScopePolicy` is not a permission itself, but it determines "who the data belongs to", which indirectly determines the boundary of permissions.

[ScopePolicy.cs](../../../SchemaNode.App/Property/App/ScopePolicy.cs#L34-L57):

```csharp
public sealed class AppScopePolicy
{
    [Meta<Default>(AppScopeType.BusinessTarget)]
    public AppScopeType Type { get; set; } = AppScopeType.BusinessTarget;

    [Relation<Visible, Call>(NODE_SELF, NS_SYSTEM_LOGIC_EQ, $"{nameof(Type)}", AppScopeType.IsolationContext)]
    public AppScopeContextMap[]? ContextMaps { get; set; }
}
```

[AppScopeType.cs](../../../SchemaNode.App/Enum/AppScopeType.cs#L11-L26):

```csharp
public enum AppScopeType
{
    BusinessTarget = 0,   // Target a business object; default
    SystemLevel = 1,      // No target; global application
    IsolationContext = 2, // Isolate by context fields, such as tenantId / orgId
}
```

Three modes correspond to three data boundaries:

```text
SystemLevel        → globally shared (foundational applications such as users and organizations)
BusinessTarget     → one copy of data per target (campaigns, projects)
IsolationContext   → one copy of data per context combination (tenant, organization isolation)
```

`ContextMaps` is visible only under `IsolationContext`; it maps context fields to scope columns in the table. When saving a field, it is validated that they cannot have the same names as business fields; see [SchemaStorageProviderExtension.cs](../../../SchemaNode.App/Schema/Provider/SchemaStorageProviderExtension.cs#L293-L327).

---

## 8. Summary

```text
Auths
  ├── PolicyScope (9 kinds)
  ├── inheritance chain (parent first)
  ├── evaluator (parameterless bool)
  └── per-request cache

RowAuths
  ├── Evaluator → whether it applies
  ├── Filter → read: query condition / write: row-by-row validation
  └── fail closed

ColAuths
  ├── configured by column
  ├── multiple evaluators, any one passing
  └── result pruning

ScopePolicy
  ├── SystemLevel
  ├── BusinessTarget
  └── IsolationContext
```

The key of the permission layer is not "how many checkpoints there are", but three things:

1. **Permissions are declarative**: evaluators and filters are semantic functions;
2. **Permissions are inherited**: parent configuration automatically covers children;
3. **Permissions are merged before execution**: read permissions become query conditions, write permissions become pre-validation.

The next chapter enters the derivation layer: views, foreign keys, aggregation, and how, when data changes, to find the fields that need recomputation along the lineage.
