# 01. App Dialect Overview

SchemaNode.Core provides the definition domain, the public semantic consensus, and the semantic runtime, but it does not itself provide the execution environment that a business application needs.

**SchemaNode.App is a semantic dialect built on top of Core**: it elevates execution capabilities such as application containers, data fields, permissions, workflows, events, and data access into a family of prototypes that can be semantically described, composed, and executed.

This chapter does not go into details; it first lays out the global map: what parts the App dialect consists of, how they are assembled, and which chains a business request passes through. Later chapters then unfold them one by one along the code.

---

## 1. Where App Sits in the Layers

```text
              Definition Domain
                     ↓
            Public Semantic Consensus
                     ↓
              ┌──────┴──────┐
              ↓             ↓
          App Dialect    Other Dialects
              ↓
   ┌──────────┼──────────┬───────────┐
   ↓          ↓          ↓           ↓
Application  Field   Workflow     Event
   ↓          ↓          ↓           ↓
App Runtime  Data     Scheduler   Dispatcher
```

Core only knows the common prototypes `node`, `struct`, `array`, and `function`.
App adds its own prototypes on top:

| SchemaKind | Constant | Description |
| --- | --- | --- |
| `app` | `SCHEMA_KIND_APP` | Application container, nestable to form application namespaces |
| `app.field` | `SCHEMA_KIND_APP_FIELD` | Application field, a logical data object |
| `app.workflow` | `SCHEMA_KIND_APP_WORKFLOW` | Workflow definition |
| `app.workflow.node` | `SCHEMA_KIND_APP_WORKFLOW_NODE` | Workflow node |
| `node.event` | `SCHEMA_KIND_NODE_EVENT` | Event declaration |
| `node.workflow` | `SCHEMA_KIND_NODE_WORKFLOW` | Workflow node type |

These constants are centralized in [AppConstant.cs](../../../SchemaNode.App/Utility/AppConstant.cs#L11-L45).


---

## 2. Three Core Definitions

The App dialect has only three entry types for its semantic definitions; every other capability is attached to them through Properties.

### 2.1 AppSchema: Application Container

[AppSchema.cs](../../../SchemaNode.App/Schema/App/AppSchema.cs#L24-L58) defines the application itself:

```csharp
[Meta<SchemaKind>(SCHEMA_KIND_APP, SCHEMA_KIND_ORDER_APP)]
[Meta<Append>(typeof(Display), typeof(Description), typeof(Relations), typeof(SystemDefined))]
public sealed class AppKind;

[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP}.schema")]
[Meta<Attach>(SCHEMA_KIND_APP)]
public sealed class AppSchema: PropertyOwner, IErrorProvider
{
    public string? Container { get; set; }
    public required string Name { get; set; }
    public string FullName => $"{Container}.{Name}".Trim('.');
}
```

Key points:

- `Container` points to the parent application (its type is `AppType`), so applications are naturally nestable;
- `Name` is the unique identifier within an application, and `FullName` is `Container.Name`;
- `Append` declares the public properties an application may carry (display name, description, relations, system-defined marker);
- `Attach` lets the properties of the App dialect attach to this SchemaKind.

`AppSchema` also carries runtime state: `Apps`, `Fields`, `Workflows`, `NodeSchemas`, `Provider`, `LoadState`, and `Error`. These fields are never written into the definition; they are only used for loading and merging.

### 2.2 AppFieldSchema: Logical Data Object

[AppFieldSchema.cs](../../../SchemaNode.App/Schema/App/AppFieldSchema.cs#L22-L61) defines a field:

```csharp
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP_FIELD}.schema")]
[Meta<TypeProvider>(nameof(Type))]
[Meta<AppProvider>(nameof(App))]
public sealed class AppFieldSchema: PropertyOwner, IErrorProvider
{
    public string App { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }   // Points to a semantic type, usually array
}
```

A field is not a table in the traditional sense; it is "a data entity in an application execution environment that can be semantically described and processed". Its `Type` is usually an array type: the array elements provide the primary key and field structure, and the field then uses Properties such as `View`, `Foreigns`, and `DataDerive` to decide where the data comes from, how it is related, and how it is derived. Of course, a field can also be another data type, such as `struct`, for single-row data.

### 2.3 AppWorkflowSchema: Workflow Definition

[AppWorkflowSchema.cs](../../../SchemaNode.App/Schema/App/AppWorkflowSchema.cs#L27-L121) defines workflows and nodes:

```csharp
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP_WORKFLOW}.schema")]
public sealed class AppWorkflowSchema: PropertyOwner
{
    public string App { get; set; }
    public string Name { get; set; }
    public bool Active { get; set; }
    public AppWorkflowNodeSchema[] Nodes { get; set; } = [];
}

[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP_WORKFLOW}.node")]
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

Nodes form a directed acyclic graph through `Previous`. `Payload` is not filled in by hand; it is derived by a `Relation` from the node type's semantic function (unfolded in the next chapter).

---

## 3. Module Map

The App project is organized into the following directories by responsibility:

| Directory | Responsibility | Key types |
| --- | --- | --- |
| `Schema/App` | Definition bodies for applications, fields, and workflows | `AppSchema`, `AppFieldSchema`, `AppWorkflowSchema` |
| `Schema/Type` | Type declarations for events and workflows | `EventSchema`, `WorkflowSchema` |
| `Schema/Provider` | Reading, storing, and merging definitions | `IAppEntryProvider`, `IAppEntryStorageProvider`, `DynamicAppEntryStorageProvider` |
| `Runtime/App` | Runtime objects and loading | `AppType`, `AppFieldType`, `AppWorkflowType` |
| `Runtime/Compile` | Compile-time context | `DataDeriveCompileContext` |
| `Property/App` | App dialect properties | `Auths`, `RowAuths`, `ColAuths`, `View`, `Foreigns`, `DataDerive`, `ScopePolicy`, etc. |
| `Data` | Data access abstraction and querying | `IAppDataProvider`, `DynamicTableSchema`, `AppSchemaDataFilter` |
| `Data/Entity` | Storing application definitions with application data | `AppEntity`, `AppFieldEntity`, `NodeEntity` |
| `Data/Sql` | SQL dialect support | `IAppDataSqlProvider<T>`, `ISqlProvider` |
| `Workflow` | Workflow execution | `BaseWorkflow`, `WorkflowContext`, `DefaultWorkflowScheduler`, `Control/*` |
| `Event` | Events and dispatching | `BaseEvent`, `AppEvent`, `SchemaEvent`, `DefaultEventDispatcher` |
| `Http` | HTTP protocol and API infrastructure | `SchemaApi`, `ISchemaApiProtocol` |
| `Api` | Concrete APIs | `Api/Schema/Edit`, `Api/Schema/Info`, `Api/Application` |
| `Function` | Built-in semantic functions | `SystemReflectApp`, `SystemReflectWorkflow`, `SystemReflectEvent`, `SystemAppData` |
| `Generator` | Generating Schema from types | `WorkflowGenerator`, `EventGenerator` |
| `Service` | Runtime assembly | `AppService`, `AppRuntimeStageHandler` |
| `Attribute`, `Enum`, `Utility` | Annotations, enums, constants, and conversions | `AppScopeType`, `AppConstant` |

---

## 4. Runtime Assembly

The entry point of the App dialect is [AppService.cs](../../../SchemaNode.App/Service/AppService.cs#L55-L64):

```csharp
public static IServiceCollection AddAppSchemaAssemblies(this IServiceCollection services, params Assembly[] assemblies)
{
    services.TryAddSingleton<ISchemaRuntime, AppSchemaRuntime>();
    services.TryAddEnumerable(ServiceDescriptor.Scoped<IRuntimeStageHandler, AppRuntimeStageHandler>());
    return services.AddSchemaAssembly<AppSchemaRuntime>().AddSchemaAssemblies(assemblies);
}
```

This code does two things:

1. It replaces Core's default `SchemaRuntime` with `AppSchemaRuntime`. AppRuntime additionally maintains the application tree, field index, and event and workflow generators.
2. It registers `AppRuntimeStageHandler`, letting the App dialect hook into Core's runtime stage callbacks.

`AppRuntimeStageHandler` intervenes at two stages:

```csharp
// After system types are loaded: scan [App] annotations and generate system applications and fields
public async Task OnSystemSchemaLoaded(ISchemaContext context, IEnumerable<Assembly> assemblies)

// Before user Schema is loaded: reset the application tree so system Schema and custom Schema do not mix
public Task OnSchemaLoadingAsync(ISchemaContext context)
```

See [AppRuntimeStageHandler.cs](../../../SchemaNode.App/Service/AppRuntimeStageHandler.cs#L115-L230).

In addition, it registers a batch of execution-layer infrastructure in `OnServiceInitialization`:

- the critical region `ICriticalRegionProvider`;
- the Quartz scheduler (scheduled tasks);
- `WorkflowContext`;
- `ISchemaApiProtocol` (the default HTTP protocol);
- scanning all `SchemaApi<,>` subclasses and registering them as APIs;
- `IEventDispatcher<BaseEvent>`, `IWorkflowScheduler`, `IWorkflowContextPersistence`;
- `Access` and `AccessContextItemProvider` (request context).

See [AppRuntimeStageHandler.cs](../../../SchemaNode.App/Service/AppRuntimeStageHandler.cs#L27-L110).

---

## 5. Four Main Execution Chains

The App code can be understood along four chains: definition → loading → access → execution:

```text
Definition chain
  AppSchema / AppFieldSchema / AppWorkflowSchema
        ↓
  IAppEntryProvider ← IAppEntryStorageProvider
        ↓
  AppSchemaRuntime (application tree, field index, events, workflows)

Load chain
  AppSchemaRuntime
        ↓
  AppType → AppFieldType → AppWorkflowType
        ↓
  properties, relations, permissions, views, derivation information

Access chain
  SchemaContext
        ↓
  AuthorizeAsync (permissions)
        ↓
  PrepareFieldDataAsync (dynamic table)
        ↓
  IAppDataProvider (query / save / delete)
        ↓
  AppSchemaDataFilter (row-level filtering)

Execution chain
  BaseEvent → DefaultEventDispatcher
        ↓
  AppWorkflowType → WorkflowContext
        ↓
  BaseWorkflow node → function / control / waiting for event
```

Each chapter unfolds along one of these chains.

---

## 6. The Full Projection of a Field

To tie the chains above together, it helps to first see which objects a field passes through from definition to execution:

```text
AppFieldSchema
  ├── App            → AppType
  ├── Type           → ArrayType → StructType (field structure)
  ├── Display        → editor display
  ├── View           → cross-application view
  ├── Foreigns       → foreign key relations
  ├── DataDerive     → data source and computation
  ├── Auths          → field-level permission
  ├── RowAuths       → row-level permission
  ├── ColAuths       → column-level permission
  ├── Pageable       → pagination configuration
  ├── EnableStorage  → whether to persist
  └── TableName      → physical table name
        ↓
AppFieldType
  ├── ValueType      → value type
  ├── DynamicTable   → dynamic table structure
  ├── ViewSource     → view source
  ├── PushSource     → push source
  ├── BlockColumns   → column blacklist
  └── GetSchemaAsync → outward Schema projection
        ↓
IAppDataProvider
  ├── QueryDynamicTableDataAsync
  ├── SaveDynamicTableDataAsync
  ├── DeleteDynamicTableDataAsync
  └── DropDynamicTableAsync
```

This is also how the subsequent chapters of the App documentation are organized: definition, runtime, data, permissions, derivation, workflow, events, and API.

---

## 7. Reading Guide

| Chapter | Focus |
| --- | --- |
| [02. Application Semantic Definitions](02-schema.md) | How definitions are written, where they live, and how they merge |
| [03. Runtime Loading](03-runtime.md) | How `AppType` / `AppFieldType` are loaded and reloaded |
| [04. Data Layer](04-data.md) | Dynamic tables, Providers, CRUD |
| [05. Query and Filtering](05-query-filter.md) | Query tree, row permission filtering, pagination |
| [06. Permissions](06-auth.md) | Inherited permissions, Schema / Data permissions |
| [07. Views, Foreign Keys, and Derive](07-derive.md) | Foreign keys, views, aggregation, lineage |
| [08. Workflow](08-workflow.md) | Node graph, context, scheduling |
| [09. Events](09-event.md) | Event declarations, dispatching, topic matching |
| [10. HTTP API and Extension Points](10-http-api.md) | API protocol, function namespaces, extension points |

> App is only one dialect implementation, not a fixed shape of Core. When reading, focus on "how execution capabilities are abstracted into semantics" rather than memorizing the name of every Property.
