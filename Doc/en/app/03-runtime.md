# 03. Runtime Loading

Definitions are only text and structure. For them to take part in execution, they must first be loaded as runtime objects.

Following the call chain of `SchemaContext.GetAppTypeAsync`, this chapter explains how App turns `AppSchema` into `AppType`, `AppFieldType`, and `AppWorkflowType`, and which information is fixed, cached, or projected outward during loading.

---

## 1. Two Trees

Core's `SchemaRuntime` maintains the type tree: namespaces, types, enums, and relations.

App's `AppSchemaRuntime` inherits it and additionally maintains the application tree:

```text
SchemaRuntime
  ├── RootNamespace
  ├── NodeTypes (types)
  └── ...

AppSchemaRuntime : SchemaRuntime
  ├── _rootAppSchema    → system application tree ([App] scan result)
  ├── _apps             → system application runtime objects
  ├── _appFieldTypes    → system field type index
  ├── _typeAppFields    → type → (application, field) reverse lookup
  └── RootAppType       → root of the user application tree
```

See [AppSchemaRuntime.cs](../../../SchemaNode.App/Context/AppSchemaRuntime.cs#L16-L24).

The two roots are deliberately separated:

- `_rootAppSchema` / `_apps` carry only system applications, originating from `[App]` annotations;
- `RootAppType` carries user-defined applications, originating from Providers and storage.

`GetSystemAppField(Type)` provides a reverse lookup from "type → system field", which AI tools or data push can use to determine which application field a given C# type corresponds to.

---

## 2. Loading Entry Point of an Application

`GetAppTypeAsync` is a loader that drills down segment by segment through the namespace; see [SchemaContext.cs](../../../SchemaNode.App/Context/SchemaContext.cs#L23-L71):

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

For example, `fullName` = `activity.activity` first loads `activity` and then `activity.activity`. Each segment goes through the same logic:

```text
LoadAppTypeAsync(root, span)
  ├── already loaded and not reload   → return directly
  ├── LoadAppSchemaAsync  → obtain AppSchema
  ├── create RuntimeAppType
  ├── detach schema.Apps first to avoid recursive loading
  ├── mark Loaded = true
  ├── result.LoadAsync(context, schema)
  └── attach child application schemas back to the container
```

Detaching child applications before loading prevents loading a parent application from recursively triggering all of its children.

### 2.1 Priority of Schema Sources

`LoadAppSchemaAsync` decides where an application's Schema comes from; see [SchemaContext.cs](../../../SchemaNode.App/Context/SchemaContext.cs#L73-L113):

```text
1. system application (_rootAppSchema)         → LoadState = System
2. context.SystemMode == true                  → use system applications only
3. each IAppEntryProvider.GetAppSchemaAsync
       ├── LoadState = Service
       ├── if a system Schema already exists, merge the system part into the Provider result
       └── merge strategy: loadSchema.Combine(schema)
```

The resulting value is tagged by its source via LoadState, and later reloads use it to decide whether to skip the system part.

This also explains why App definitions can come simultaneously from code (system applications), a database (the Dynamic provider), and third-party Providers without overwriting each other.

---

## 3. Loading AppType

`AppType.LoadAsync`; see [AppType.cs](../../../SchemaNode.App/Runtime/App/AppType.cs#L186-L257):

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

The load order matters:

```text
Release()
  ↓  release old UsedBy relations so the type can be reloaded
Props
  ↓  LoadPropertiesAsync resolves property values and reference types
ScopePolicy
  ↓  the application's target policy (SystemLevel / BusinessTarget / IsolationContext)
Fields
  ↓  each field is loaded independently
Relations
  ↓  application-level relations (cross-field constraints)
Workflows
  ↓  reuse an already-activated instance, otherwise enqueue
ReferenceTypes → AddUsedBy
```

Several key mechanisms:

### 3.1 UsedBy and Reload

`AppType` implements `INodeTypeReLoadHandler`. When a referenced type changes, the runtime finds the applications that depend on it through `AddUsedBy(this)` and then triggers a reload.

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

See [AppType.cs](../../../SchemaNode.App/Runtime/App/AppType.cs#L255-L281).

`ResetItems` clears compilation results cached on the field (such as the dynamic table structure and the compiled push function) but keeps the field object itself, avoiding a rebuild of the whole tree.

### 3.2 Deferred Activation of Workflows

```csharp
foreach (var wf in _workflows ?? [])
{
    if (context.Runtime.Stage == RuntimeStage.Activated)
        await wf.LoadAsync(context);
    else
        (context.Runtime as SchemaRuntime)?.GetOrAddRuntimeItem<AppWorkflowQueue>().Enqueue(wf);
}
```

When the runtime is not yet `Activated` during startup, workflows are enqueued first and loaded together later by `AppRuntimeStageHandler.OnActivatingAsync`. This ensures workflows do not start before the runtime is ready.

### 3.3 Application Scope Policy

`ScopePolicy` determines the application's target dimension:

| Type | Meaning |
| --- | --- |
| `SystemLevel` | Global data, no target |
| `BusinessTarget` | Targets a business object (default) |
| `IsolationContext` | Isolated by context fields such as tenant/organization |

`GetScopeContextItemTypes` converts `ContextMaps` into actual context fields and fills in a default target field when the target is missing; see [AppType.cs](../../../SchemaNode.App/Runtime/App/AppType.cs#L465-L532).

---

## 4. Loading AppFieldType

Field loading is the most complex part of the App runtime. `LoadAsync` (see [AppFieldType.cs](../../../SchemaNode.App/Runtime/App/AppFieldType.cs#L223-L403)) can be divided into six steps in order:

```text
1. ValueType           → resolve Type
2. Properties          → resolve field properties, producing _refTypes / Error
3. Storage             → Disable / EnableStorage / Topology / TableName / Pageable / Filters
4. Primary             → for struct arrays, resolve the C# properties corresponding to the primary key
5. DataDerive          → resolve push functions, observers, third-party fields
6. Foreigns / View     → validate foreign keys and views
```

### 4.1 Storage-Related Properties

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

`EnableDynamicTable` is the switch for the later data layer: a dynamic table is created only when storage is enabled and the field is not disabled.

### 4.2 Primary Key

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

Only "system-defined structs" (types declared in C#) resolve C# primary keys; types defined online by users have no corresponding C# type, so their primary keys are handled through the semantic layer.

### 4.3 Data Derivation and Observers

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

See [AppFieldType.cs](../../../SchemaNode.App/Runtime/App/AppFieldType.cs#L260-L310).

This code establishes the lineage chain:

```text
source field AddObserver(derived field)
third-party field AddObserver(derived field)
derived field records PushSource / ThirdPushFields / PushFuncSchema
```

Any subsequent change to a source field can be traced along the observer chain to the fields that need recomputation.

### 4.4 Foreign Key and View Validation

A foreign key requires that the referenced application is not `SystemLevel` and that the referenced field exists:

```csharp
if (string.IsNullOrWhiteSpace(foreign.Field) || string.IsNullOrWhiteSpace(foreign.App) ||
    await context.GetAppTypeAsync(foreign.App) is not { } refApp ||
    refApp.ScopeType == AppScopeType.SystemLevel ||
    structType?.GetField(foreign.Field) == null)
{
    Error ??= AppErrorCodes.APP_FIELD_FOREIGN_NOT_VALID;
}
```

View validation is stricter: the source field structure must match, and a `DisplayOnly` field must either be generated by a relation or come from a same-named field in the source application:

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

See [AppFieldType.cs](../../../SchemaNode.App/Runtime/App/AppFieldType.cs#L317-L402).

---

## 5. Outward Projection

Runtime objects do not expose their internal state directly to the front end. `GetSchemaAsync` generates an "editable Schema carrying permission flags".

### 5.1 Projection of AppType

See [AppType.cs](../../../SchemaNode.App/Runtime/App/AppType.cs#L315-L350):

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

- When `noAuth = true`, permissions are not computed; used for internal export or system tasks;
- System applications cannot be created or deleted, so `SchemaCreate` / `SchemaDelete` are directly `false`.

### 5.2 Projection of AppFieldType

Field projection adds data permissions and a column blacklist; see [AppFieldType.cs](../../../SchemaNode.App/Runtime/App/AppFieldType.cs#L421-L472):

```csharp
schema.SetProperty<DataCreate, bool>(!isSystem && !ReadOnly && await context.AuthorizeAsync(this, PolicyScope.DataCreate, true));
schema.SetProperty<DataRead, bool>(!isSystem && await context.AuthorizeAsync(this, PolicyScope.DataRead, true));
schema.SetProperty<DataUpdate, bool>(!isSystem && !ReadOnly && await context.AuthorizeAsync(this, PolicyScope.DataUpdate, true));
schema.SetProperty<DataDelete, bool>(!isSystem && !ReadOnly && await context.AuthorizeAsync(this, PolicyScope.DataDelete, true));
```

Column permissions are evaluated per field through `GetColPolicies(f.Name)`, and fields that fail go directly into `BlockColumns`:

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

`BlockColumns` is the execution form of "column-level permission": the front end hides the columns, and the backend removes them from query results.

---

## 6. Workflow Loading and Activation

`AppWorkflowType` has two stages.

### 6.1 LoadAsync: Resolving Nodes

See [AppWorkflowType.cs](../../../SchemaNode.App/Runtime/App/AppWorkflowType.cs#L105-L127):

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

This step only resolves properties and payload types; it does not create execution instances.

### 6.2 ActiveAsync: Building the Graph and Restoring

See [AppWorkflowType.cs](../../../SchemaNode.App/Runtime/App/AppWorkflowType.cs#L213-L327):

```text
for each node:
  ├── get the C# type of WorkflowType
  ├── Activator.CreateInstance
  ├── set Application / Name / Fork / ForkKey / ...
  ├── inject State
  ├── wNode.LoadAsync(context, node)
  ├── bind Args
  └── connect Previous / Next

validate that the entry node is unique
  ↓
create WorkflowContext
  ↓
try to restore a Running snapshot from IWorkflowContextPersistence
  ↓
otherwise Initialize(workflow, entry)
```

Two constraints are enforced here:

- The entry node must be unique (`topNodes.Count != 1` throws);
- An activated workflow cannot be modified directly when its definition is saved; see [SchemaStorageProviderExtension.cs](../../../SchemaNode.App/Schema/Provider/SchemaStorageProviderExtension.cs#L396-L419).

The `Active` flag is persisted, so after a restart the workflow can be activated again and its context restored.

---

## 7. Reload Trigger Points

Reloading is not an explicit "refresh" button; it is triggered automatically at several points:

| Trigger point | Behavior |
| --- | --- |
| Saving an application / field / workflow | `GetAppTypeAsync(name, reload: true)` |
| Type loading completes | `AppType.OnNodeTypeLoaded` → field `ResetItems` |
| Before loading user Schema | `OnSchemaLoadingAsync` → `RootAppType.ResetLoadState()` |
| Saving a system application | `SaveAppSchema` marks `Loaded = false` |

`ResetLoadState` recursively marks the entire application tree as unloaded; see [AppType.cs](../../../SchemaNode.App/Runtime/App/AppType.cs#L172-L177). It separates the "system phase" from the "user phase" so system Schema and user Schema do not mix in the same tree.

---

## 8. Summary

```text
GetAppTypeAsync
  ↓
LoadAppSchemaAsync (system → Provider → Combine)
  ↓
AppType.LoadAsync
  ├── properties / ScopePolicy
  ├── AppFieldType.LoadAsync
  │     ├── storage properties / primary key
  │     ├── DataDerive → observers and third-party fields
  │     └── Foreigns / View validation
  ├── application-level relations
  └── AppWorkflowType (enqueue or load immediately)
        ↓
      ActiveAsync (build graph, entry validation, context restore)
```

The purpose of runtime objects is not to "copy definitions" but to resolve the references in a definition into executable objects: types, functions, observers, table structures, and node graphs.

The next chapter moves to the data layer to see how these objects land on actual tables and queries.
