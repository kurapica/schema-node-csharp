# 02. Application Semantic Definitions

The previous chapter gave a map of the App dialect. This chapter enters the definition layer: in what form applications, fields, and workflows exist, where they are stored, and how they are composed.

The focus is not a list of fields but three questions:

1. Why is the App definition body so "thin"?
2. How do definitions enter the runtime from external sources?
3. How are same-named definitions from multiple sources merged?

---

## 1. Definition Bodies Describe Identity Only; Semantics Hang on Properties

`AppSchema`, `AppFieldSchema`, and `AppWorkflowSchema` all inherit from `PropertyOwner`. This means one part of the semantics is written directly in the definition's properties (identity, structure), while the other part is attached in the form of Properties.

```text
AppSchema
 ├── Container / Name      (identity, Meta)
 ├── Apps / Fields / Workflows / NodeSchemas (runtime state, SchemaIgnore)
 └── Property
      ├── Display
      ├── Description
      ├── Relations
      ├── SystemDefined
      ├── ScopePolicy
      └── Auths / Data* / Schema*
```

This split keeps the "definition" stable while dialect capabilities can keep being added. For example, permission-related properties such as `Auths` and `DataCreate` do not require modifying the fields of `AppSchema` itself.

### 1.1 Identity and Container of AppSchema

See [AppSchema.cs](../../../SchemaNode.App/Schema/App/AppSchema.cs#L24-L58):

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

- `PrimaryIndex` gives the application a composite primary key `(Container, Name)` at the storage layer;
- `Container` is of type `AppType`, so a parent application is a valid application reference;
- `Append` decides "which public properties an application may own", and `Attach` decides that these properties may attach to the `app` Kind;
- `SystemDefined` distinguishes system-generated applications from user applications.

`Apps`, `Fields`, `Workflows`, and `NodeSchemas` in `AppSchema` are all marked `SchemaIgnore`; they are not editable semantic fields but loading results:

```text
AppSchema
  ├── Apps         → child application set (loaded recursively by the Provider)
  ├── Fields       → field set (sorted by Seqno)
  ├── Workflows    → workflow set
  └── NodeSchemas  → types imported/exported together with the application
```

### 1.2 Application Fields: Why Type Is Usually array

See [AppFieldSchema.cs](../../../SchemaNode.App/Schema/App/AppFieldSchema.cs#L22-L61):

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

There are four key Metas here:

| Meta | Effect |
| --- | --- |
| `TypeProvider` | Specifies that `Type` is the field's value type source |
| `EntrySourceProvider` | Obtains the field's internal access paths through the array's `getelementaccessentries` |
| `AccessValueTypeProvider` | Obtains the element's access value type through the array's `getelementaccessvaluetype` |
| `AppProvider` | Specifies that `App` is the owning application, used for permission inheritance and path resolution |

Therefore `Type` is usually `array<struct<...>>`. The array elements provide the primary key and field structure, and the field itself represents "the collection of these records". This also explains why properties such as `DataDerive`, `View`, and `Foreigns` all operate at the array-element level.

`App` and `Name` are marked as `PrimaryIndex(0)` and `PrimaryIndex(1)` respectively; a field is unique within its application, and `Name` is immutable (`Immutable`).

### 1.3 Workflow Nodes: The Graph Structure Lives in the Definition

What matters in [AppWorkflowSchema.cs](../../../SchemaNode.App/Schema/App/AppWorkflowSchema.cs#L27-L177) is not the node fields but several relations:

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

- `Previous` declares predecessor nodes, and the runtime builds the graph from it;
- `Payload` carries `DisplayOnly` and is derived from `Type` by a `Relation`, so users cannot fill it in directly;
- `Fork` / `ForkKey` are only visible when the node type supports forking;
- The type of `State` is derived from the node type's `getstatetype`.

`AppWorkflowSchema` itself also declares `EntrySourceProvider`, letting access paths unfold along `Previous` and `Nodes`. This is a typical use of "path is semantics" in SchemaNode: the access paths of the node graph are not special-case code but declared by relations.

---

## 2. Events and Workflow Node Types Are Also Schemas

The App dialect provides not only the `app.*` SchemaKinds but also `node.event` and `node.workflow`. They are definitions of "node types".

### 2.1 Event Declarations

[EventSchema.cs](../../../SchemaNode.App/Schema/Type/EventSchema.cs#L19-L41):

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

Event types are declared in C# and generated into Schema by `EventGenerator`, with `Payload` specifying the payload type. `Args` is used to construct the topic (Topic); this is unfolded in the events chapter.

### 2.2 Workflow Node Types

[WorkflowSchema.cs](../../../SchemaNode.App/Schema/Type/WorkflowSchema.cs#L19-L59):

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

The five semantic slots correspond to:

```text
Kind      → node category (workflow / call / event / interaction)
Payload   → node output type
Settings  → global configuration type at workflow creation
Session   → session state type during node execution
Args      → node arguments
```

Workflow node types differ from ordinary types: they are more tightly coupled with execution, so they are usually declared in C# and then exposed to the front end through the Generator.

---

## 3. Where Definitions Come From

App does not require definitions to be stored in a database. The source of definitions is isolated behind two interfaces:

```text
IAppEntryProvider
  ├── GetSchemaAsync(names)        (NodeSchema)
  ├── GetEnumEntryAccessAsync      (enum entries)
  └── GetAppSchemaAsync(app)       (AppSchema)

IAppEntryStorageProvider : IAppEntryProvider
  ├── SaveSchemaAsync / DeleteSchemaAsync
  ├── SaveEnumEntriesAsync
  ├── SaveAppSchemaAsync / DeleteAppSchemaAsync
  ├── SaveAppFieldSchemaAsync / DeleteAppFieldSchemaAsync / SwapAppFieldSchemaAsync
  └── SaveAppWorkflowSchemaAsync / DeleteAppWorkflowSchemaAsync
```

See [IAppEntryProvider.cs](../../../SchemaNode.App/Schema/Provider/IAppEntryProvider.cs) and [IAppEntryStorageProvider.cs](../../../SchemaNode.App/Schema/Provider/IAppEntryStorageProvider.cs#L8-L72).

This split yields two deployment forms:

- Implementing only `IAppEntryProvider`: definitions are read-only (for example, from files, a remote configuration center, or code generation);
- Implementing `IAppEntryStorageProvider` as well: definitions can be edited online and persisted.

### 3.1 Storing Application Definitions with the Application's Own Data

The built-in [DynamicAppEntryStorageProvider.cs](../../../SchemaNode.App/Schema/Provider/DynamicAppEntryStorageProvider.cs#L403-L418) stores Schema in `IAppDataProvider` and accesses it through Entities:

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

Three points:

1. It does not write SQL directly but reuses the Entity capabilities of `SchemaContext`;
2. `Target` is fixed to `Guid.Empty`, denoting the system scope, isolated from business tenant data;
3. During loading it recursively queries child applications, fields, and workflows by `Container` and sorts by `Seqno`; see [GetAppSchemaAsync](../../../SchemaNode.App/Schema/Provider/DynamicAppEntryStorageProvider.cs#L334-L400).

`IAppEntryStorageProvider` also inherits `IAppEntryProvider`, so `AddSchemaStorageProvider<T>()` registers both at once:

```csharp
services.TryAddScoped<IAppEntryStorageProvider>(sp => sp.GetRequiredService<T>());
return services.AddAppSchemaProvider<T>();
```

See [AppService.cs](../../../SchemaNode.App/Service/AppService.cs#L25-L30).

---

## 4. The Full Chain of a Single Save

Saving is not simply "writing to the database". Take `SaveAppSchemaAsync` as an example; see [SchemaStorageProviderExtension.cs](../../../SchemaNode.App/Schema/Provider/SchemaStorageProviderExtension.cs#L178-L263):

```text
SaveAppSchemaAsync
  ├── ClearPolicyFlags          (clear permission flags passed from the front end to prevent privilege escalation)
  ├── GetAppTypeAsync           (get the existing runtime object)
  ├── AuthorizeAsync            (SchemaCreate / SchemaUpdate)
  ├── ScopePolicy change check  (changing the target policy mid-way is not allowed outside DEBUG)
  ├── Save NodeSchemas          (field types may depend on these types)
  ├── provider.SaveAppSchema    (persist)
  ├── parent app SaveAppSchema  (attach to the application tree at runtime)
  ├── GetAppTypeAsync(reload)   (force a reload)
  ├── save Fields one by one
  ├── save Workflows one by one
  └── RaiseEvent<AppSchemaChangeEvent>
```

This chain reflects two principles of the App dialect:

- **Permissions first**: every write operation goes through `AuthorizeAsync` first;
- **Definition and runtime stay in sync**: reload immediately after persisting, avoiding a state where "it is in the database but not in memory".

Saving a field has an additional layer of validation; see [SaveAppFieldSchemaAsync](../../../SchemaNode.App/Schema/Provider/SchemaStorageProviderExtension.cs#L293-L327):

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

This shows that a field type cannot be arbitrary: it must be resolvable, an array must have elements, a struct must not be empty, and it must not conflict with the application's isolation context fields.

---

## 5. Merging Multiple Sources

Application definitions can come from multiple Providers. `AppSchema.Combine` performs the merge; see [AppSchema.cs](../../../SchemaNode.App/Schema/App/AppSchema.cs#L127-L163):

```csharp
public bool Combine(AppSchema? other, ISchemaRuntime? runtime = null)
{
    if (other is null || !other.Name.Equals(Name, StringComparison.OrdinalIgnoreCase)) return false;
    CombineProperties(other, runtime, SCHEMA_KIND_APP);

    // Child applications: recursively merge those with the same name, append new ones directly
    if (other.Apps is { Length: > 0 })
    {
        if (Apps == null || Apps.Length == 0) Apps = other.Apps.ToArray();
        else Apps = Apps.Concat(other.Apps.Where(a => !Apps.Any(e => e.Combine(a))).ToArray()).ToArray();
    }

    // Fields and workflows follow the same merge rule; see L145-L161 in the source
}
```

`AppFieldSchema.Combine` additionally requires `Type` to be consistent; see [AppFieldSchema.cs](../../../SchemaNode.App/Schema/App/AppFieldSchema.cs#L80-L86):

```csharp
if (other is null || !other.Name.Equals(Name, StringComparison.OrdinalIgnoreCase)) return false;
if (other.Type.Equals(Type, StringComparison.OrdinalIgnoreCase))
    CombineProperties(other, runtime, SCHEMA_KIND_APP_FIELD);
return true;
```

That is, same-named fields may accumulate properties but cannot silently change their type. The workflow `Combine` is stricter and only allows same-named entries; see [AppWorkflowSchema.cs](../../../SchemaNode.App/Schema/App/AppWorkflowSchema.cs#L74-L77).

This provides a composition mechanism for "base application + tenant overrides + plugin extensions" without having to turn definitions into one large configuration.

---

## 6. System Applications and Automatic Generation

System applications are not written by hand. `AppRuntimeStageHandler.OnSystemSchemaLoaded` scans the types in the assemblies, and for every type annotated with `[App]` it generates the corresponding application and fields:

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

See [AppRuntimeStageHandler.cs](../../../SchemaNode.App/Service/AppRuntimeStageHandler.cs#L119-L196).

Two details in the generation process are worth noting:

1. **Field types prefer arrays**: `runtime.GetSystemArraySchema(schemaType, true) ?? schemaType`. If the type has a primary key, it is automatically wrapped as an array field; otherwise the scalar/struct is used directly.
2. **`[DataDerive]` automatically fills in Combine**: if the field type is a struct, the `[DataCombine]` on the struct's fields are collected into `Derive.Combines`; see `L166-L193` in the same file.

This makes "declaring a system application in C#" a viable development approach, without having to write JSON.

---

## 7. JSON Import/Export and Validation

Definitions are ultimately migrated between environments as JSON. Serialization must preserve extension properties (`display`, `struct`, `enum`, `func`, etc.), so the default serializer configuration cannot be used. The project exposes a serialization entry point consistent with the HTTP API in [SchemaNodeJsonExtension.cs](../../../SchemaNode.App/Schema/SchemaNodeJsonExtension.cs#L13-L44):

```csharp
public static JsonSerializerOptions GetSerializerOptions() => JsonOptions.GetJsonOptions(false);

public static NodeSchema? ParseNodeSchema(this SchemaContext context, JsonNode? node)
    => context.FromJsonRequest<NodeSchema>(node.ToJsonString());

public static JsonNode? ToSchemaJson(this SchemaContext context, NodeSchema? schema)
    => schema?.ToJsonNode();
```

External tools (for example, AI chat, MCP tools, and the `validate-schema` API) can use [SchemaCandidateValidator.cs](../../../SchemaNode.App/Schema/Validation/SchemaCandidateValidator.cs#L24-L94) for unified validation. It is not a second parser but reuses the runtime type definitions:

```csharp
await ValidateCoreAsync(context, normalized, expectedName, checkAuthorization, result, 0, cancellationToken);
```

Three kinds of normalization are also performed before validation:

- `NormalizeLocaleStrings`: converts string-form display names into locale objects;
- `NormalizeTypeAliases`: converts short names such as `string` and `int` into canonical type names such as `system.string`;
- `NormalizeFlattenedDefinitions`: supports flattened extension properties.

This lets "model-generated Schema" and "editor-saved Schema" go through the same set of rules.

---

## 8. Summary

The definition layer of App can be summarized as:

```text
Identity (Meta) + Structure (fields) + Semantics (Property) + Source (Provider) + Merge (Combine)

AppSchema        → application container, nestable
AppFieldSchema   → logical data object in array form
AppWorkflowSchema→ directed acyclic graph + node types
EventSchema      → event declaration (node.event)
WorkflowSchema   → workflow node type (node.workflow)
```

The next chapter moves to the runtime: how these definitions are loaded into `AppType`, `AppFieldType`, and `AppWorkflowType`, and how they project Schema outward.
