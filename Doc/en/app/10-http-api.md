# 10. HTTP API and Extension Points

The preceding chapters covered definitions, runtime, data, permissions, derivation, workflows, and events separately. This chapter strings them together into an external HTTP service and explains how to extend it.

---

## 1. The API Abstraction

### 1.1 SchemaApi

All APIs inherit the same generic base class, see [SchemaApi.cs](../../../SchemaNode.App/Http/SchemaApi.cs#L11-L78):

```csharp
public abstract class SchemaApi<TRequest, TResponse>
    where TRequest : SchemaApiRequest
    where TResponse : SchemaApiResponse
{
    public async Task<TResponse?> _ExecuteAsync(TRequest request, ILogger logger)
    {
        Services = request.Context!.RequestServices;
        Logger = logger;
        _criticalRegionProvider = new Lazy<ICriticalRegionProvider>(...);
        SchemaContext = Services.GetRequiredService<SchemaContext>();
        return await ExecuteAsync(request, request.Context.RequestAborted);
    }

    protected virtual Task<TResponse?> ExecuteAsync(TRequest request, CancellationToken cancellationToken)
        => Task.FromResult(default(TResponse));
}
```

The conventions are clear:

- One API handles exactly one request type and one response type;
- APIs access the runtime through `SchemaContext`;
- When mutual exclusion is needed, use `GetLockAsync`, which wraps `ICriticalRegionProvider`.

### 1.2 Requests and Responses

The request base class carries common context, see [SchemaApi.cs](../../../SchemaNode.App/Http/SchemaApi.cs#L85-L113):

```csharp
public abstract class SchemaApiRequest
{
    [JsonIgnore] public HttpContext? Context { get; set; }
    [JsonIgnore] public IFormFileCollection? Files { get; set; }
    public string? Locale { get; set; }
    public DateFormatMode? DateFormat { get; set; }
    public string? TimeZone { get; set; }
}
```

The response base class, conversely, carries execution information, see [SchemaApi.cs](../../../SchemaNode.App/Http/SchemaApi.cs#L118-L161):

```csharp
public abstract class SchemaApiResponse
{
    [JsonIgnore] public SchemaApiFile? Output { get; set; }
    public long? ExecuteTime { get; set; }
    public string? TimeZone { get; set; }
    public string? Locale { get; set; }
}
```

When `Output` is not null, the protocol layer turns it into a file download instead of a JSON response.

### 1.3 Processing Flow

`ISchemaApiProtocol.ProcessAsync` is the unified entry point, see [ISchemaApiProtocol.cs](../../../SchemaNode.App/Http/ISchemaApiProtocol.cs#L57-L203):

```text
Read body
  ├── application/json      → raw text
  └── multipart/form-data   → form fields joined into JSON + files
        ↓
Deserialize (DateFormat / TimeZone / Locale)
        ↓
DataAnnotations validation
        ↓
Execute API
        ↓
Exception mapping
  ├── UnauthorizedAccessException → 403
  └── others                      → InternalError
        ↓
Fill ExecuteTime / TimeZone / Locale
        ↓
Output JSON or file
```

The time zone and locale in the request are written into `SchemaContext`; subsequent date formatting, enum display, and error messages all follow them.

---

## 2. Protocols

A protocol is responsible for only five things:

```csharp
IOpenApiSchema WrapRequestSchema(...);
IOpenApiSchema WrapResponseSchema(...);
TRequest ReadRequest<TRequest>(...);
IResult GenerateResult<TResponse>(...);
IResult GenerateErrorResponse(...);
```

See [ISchemaApiProtocol.cs](../../../SchemaNode.App/Http/ISchemaApiProtocol.cs#L26-L53).

### 2.1 Default Protocol

[DefaultSchemaApiProtocol.cs](../../../SchemaNode.App/Http/DefaultSchemaApiProtocol.cs#L13-L32) serializes requests and responses directly, adding no envelope.

### 2.2 JSON-RPC Protocol

[JsonRpcSchemaApiProtocol.cs](../../../SchemaNode.App/Http/JsonRpcSchemaApiProtocol.cs#L14-L100) adds a standard envelope:

```json
{
  "jsonrpc": "2.0",
  "id": "uuid",
  "params": { ... }
}
```

Response:

```json
{
  "jsonrpc": "2.0",
  "id": "uuid",
  "result": { ... }
}
```

Error code mapping:

| SchemaApiErrorCode | JSON-RPC Code |
| --- | --- |
| `ParseFailed` | `ParseError` |
| `InvalidParams` | `InvalidParams` |
| `InternalError` | `InternalError` |

Switching protocols only requires:

```csharp
services.WithSchemaApiProtocol<JsonRpcSchemaApiProtocol>();
```

### 2.3 Protocol-Free APIs

APIs marked `[NoProtocol]` bypass the current protocol and always use default serialization:

```csharp
type.GetCustomAttribute<NoProtocolAttribute>() != null
```

See [Service.cs](../../../SchemaNode.App/Http/Service.cs#L194-L203).

This suits endpoints such as file downloads and health checks that should not be wrapped in an envelope.

---

## 3. Routing and Registration

### 3.1 Automatic Registration

`AppRuntimeStageHandler.OnServiceInitialization` scans all `SchemaApi<,>` subclasses:

```csharp
foreach (Type type in assemblies.SelectMany(a => a.GetTypes())
             .Where(t => t.IsSubclassOfGenericType(typeof(SchemaApi<,>)) && !t.IsAbstract))
{
    Http.Service.AddApiType(type);
    services.AddTransient(type);
}
```

See [AppRuntimeStageHandler.cs](../../../SchemaNode.App/Service/AppRuntimeStageHandler.cs#L84-L94).

### 3.2 URL Generation

`GetRequestUrl` turns the request type name into a hyphenated path, see [Service.cs](../../../SchemaNode.App/Http/Service.cs#L162-L182):

```text
BatchQueryAppDataRequest
  → batch-query-app-data
  → /schema/batch-query-app-data
```

Rules:

- Strip the `Request` suffix;
- Split at uppercase letters, lowercase each segment, and join with `-`;
- Apply the uniform `prefix`, with an optional `suffix`.

### 3.3 Conditional Registration

```csharp
if (apiType.Api.FullName!.Contains("api.schema.edit", ...))
{
    if (!hasSchemaStorage) continue;
}
else if (apiType.Api.FullName!.Contains("api.schema.application", ...))
{
    if (!hasAppDataStorage || !enableAppDataApi) continue;
}
```

See [Service.cs](../../../SchemaNode.App/Http/Service.cs#L49-L62).

The three switches determine which subset of APIs is registered:

| Condition | Effect |
| --- | --- |
| No `IAppEntryStorageProvider` | Editing APIs are not registered |
| No `IAppDataProvider` | Application data APIs are not registered |
| `enableAppDataApi = false` | Application data APIs are for demo only and off by default |

### 3.4 Swagger

`SchemaApiDocumentFilter` injects APIs into the OpenAPI document:

```csharp
var reqSchema = context.SchemaGenerator.GenerateSchema(request, context.SchemaRepository);
var resSchema = context.SchemaGenerator.GenerateSchema(response, context.SchemaRepository);

var wrappedReq = useDefaultProtocol ? reqSchema : protocol?.WrapRequestSchema(context, reqSchema) ?? reqSchema;
...
Tags = new HashSet<OpenApiTagReference> {
    new(string.Join('.', type.FullName!.Split(".").SkipLast(1).Select(s => s.ToLower())))
}
```

See [SchemaApiDocumentFilter.cs](../../../SchemaNode.App/Http/SchemaApiDocumentFilter.cs#L12-L49).

Group tags come from the type's namespace, so `Api.Schema.Edit.App.SaveAppSchema` is grouped under `api.schema.edit.app`. This is why Swagger groups APIs by capability.

---

## 4. API Groups

### 4.1 Definition Editing

| API | Purpose |
| --- | --- |
| `SaveSchema` | Save an ordinary type |
| `DeleteSchema` | Delete an ordinary type |
| `SaveEnumSubList` | Save enum sub-items |
| `SaveAppSchema` | Save an application (including NodeSchemas) |
| `DeleteAppSchema` | Delete an application |
| `SaveAppFieldSchema` | Save a field |
| `DeleteAppFieldSchema` | Delete a field |
| `SwapAppFieldSchema` | Reorder fields |
| `SaveAppWorkflowSchema` | Save a workflow |
| `DeleteAppWorkflowSchema` | Delete a workflow |
| `ToggleAppWorkflowSchema` | Activate/deactivate a workflow |

What these APIs have in common:

```text
Permission check → persist → reload runtime → raise definition change event
```

Taking `SaveAppSchema` as an example, see [SaveAppSchema.cs](../../../SchemaNode.App/Api/Schema/Edit/App/SaveAppSchema.cs#L16-L48):

```csharp
public class SaveAppSchemaRequest : SchemaApiRequest
{
    [Required] public required AppSchema Schema { get; set; }
}

protected override async Task<SaveAppSchemaResponse?> ExecuteAsync(SaveAppSchemaRequest request, CancellationToken cancellationToken)
{
    return new SaveAppSchemaResponse
    {
        Result = await SchemaContext.SaveAppSchemaAsync(request.Schema)
    };
}
```

The real logic lives in `SchemaStorageProviderExtension` (already covered in chapter 02); the API only handles deserialization and invocation.

### 4.2 Definition Query and Invocation

| API | Purpose |
| --- | --- |
| `GetSchema` | Query a type definition |
| `GetAppSchema` | Query an application definition (including fields and workflows) |
| `Protocol` | Return protocol metadata (version, capabilities) |
| `Validate` | Validate a value |
| `ValidateSchema` | Validate a candidate Schema |
| `Diagnose` | Diagnose runtime state |
| `Authorize` | Evaluate permissions |
| `CallFunction` | Invoke a semantic function |

`GetSchema` / `GetAppSchema` are the main data sources for the front-end editor. Both go through the `GetSchemaAsync` projection, so the returned permission flags and `BlockColumns` match the current user.

`Authorize` lets the front end ask "can I do this" without executing the operation, see [Authorize.cs](../../../SchemaNode.App/Api/Schema/Info/Authorize.cs).

`CallFunction` is the general entry point for semantic functions. Both parameters and return values are semantic values, and the runtime performs the type conversions.

### 4.3 Application Data

| API | Purpose |
| --- | --- |
| `BatchQueryAppData` | Batch-query field data |
| `PushAppData` | Save / delete / push data |
| `Interaction` | Submit human interaction results |
| `WorkflowInfo` | Query workflow instance status |

`BatchQueryAppData` is the main chain of chapter 05:

```text
field permissions → paging → row permissions → query → column permissions → enum scan
```

`PushAppData` is the write chain:

```text
DataCreate / DataUpdate / DataDelete permissions
  ↓
Row permission check for each row
  ↓
SaveFieldDataAsync / DeleteFieldListDataAsync
  ↓
Transaction commit → derived push → data events
```

See [PushAppData.cs](../../../SchemaNode.App/Api/Application/PushAppData.cs#L100-L200).

`WorkflowInfo` reads workflow context state, the current node, and pending interactions; it is the companion API for human nodes (`Interaction`).

---

## 5. Semantic Functions

App semantic functions are organized by namespace:

| Namespace | Class | Purpose |
| --- | --- | --- |
| `system.schema.app` | `SystemReflectApp` | Candidates, types, primary keys, and aggregate fields of applications/fields |
| `system.data.app` | `SystemAppData` | Reading and writing application data |
| `system.schema.workflow` | `SystemReflectWorkflow` | Node category, arguments, state, Payload |
| `system.schema.event` | `SystemReflectEvent` | Event arguments and payload |

### 5.1 SystemReflectApp

[App.cs](../../../SchemaNode.App/Function/App.cs#L22-L360):

```text
getaccessentries / getaccessvaluetype
getappentries / getappfields / getappfieldentries / getappfieldtype
getappfieldprimaries / getappforeignfields / hasfields
getcombinefields / getcombinetype / isscopepolicy
```

These functions mainly serve the editor: which candidates appear in a dropdown, which types a field can take, and which fields an aggregate field can group by.

### 5.2 SystemAppData

[Data.cs](../../../SchemaNode.App/Function/Data.cs#L33-L420):

```csharp
public static async Task<T?> get<T>(SchemaContext context, string app, string field, params object?[] args);
public static async Task<T?> getfield<T>(...);
public static async Task<bool> save<T>(...);
public static async Task<bool> delete<T>(...);
public static async Task<ArrayNode> getdatasource(...);
```

They are not a replacement for the APIs; they exist for "accessing data from inside a semantic function":

- `get` requires the complete primary key and checks the access scope;
- `getfield` is the implementation behind `system.data.app.getfield(...)` in derived computation;
- `save` / `delete` are subject to the same permission constraints as the APIs;
- `getdatasource` obtains source data over a time range for push computation.

### 5.3 Workflow and Event Reflection

[Workflow.cs](../../../SchemaNode.App/Function/Workflow.cs#L14-L90):

```text
iskind / hasargs / hasstate / isforkable / getpayloadtype / getstatetype
```

[Event.cs](../../../SchemaNode.App/Function/Event.cs#L7-L34):

```text
hasargs / withpayload
```

These functions let node definitions display fields dynamically according to "the semantics of the node type", instead of hard-coding the logic in the editor.

---

## 6. Extension Points

Extension of the App dialect essentially always comes down to "replace one interface or add one declaration":

| Extension target | Approach |
| --- | --- |
| Storage backend | Implement `IAppDataProvider`; for SQL scenarios also implement `IAppDataSqlProvider<T>` + `ISqlProvider` |
| Definition source | Implement `IAppEntryProvider`; implement `IAppEntryStorageProvider` if online editing is needed |
| Application composition | Multiple providers + `AppSchema.Combine` |
| Workflow nodes | Inherit `BaseWorkflow` and declare `[Meta<SchemaType>]` and `[Meta<OfNodeKind>]` |
| Events | Inherit `BaseEvent`, implement `IEventPayload<T>`, optionally `[Meta<PayloadType>]` |
| Event transport | Implement `IEventDispatcher<BaseEvent>` (e.g. Kafka / RabbitMQ) |
| API | Inherit `SchemaApi<TRequest, TResponse>` |
| API protocol | Implement `ISchemaApiProtocol` and switch with `WithSchemaApiProtocol<T>` |
| Serialization format | Implement `ISchemaFormatProvider` (default `JsonSchemaFormatProvider`) |
| Critical region | Implement `ICriticalRegionProvider` (in-process by default) |
| Workflow persistence | Implement `IWorkflowContextPersistence` |
| Workflow scheduling | Implement `IWorkflowScheduler` |
| Access context | Provide `Access` and `AccessContextItemProvider` |

### 6.1 Registration Entry

All these extensions are ultimately registered through `DI`; the main entry point is:

```csharp
services.AddAppSchemaAssemblies(typeof(Program).Assembly);
```

plus the additional registrations added as needed:

```csharp
services.AddAppDataProvider<MySqlAppDataProvider>();
services.AddSchemaStorageProvider<DynamicAppEntryStorageProvider>();
services.WithSchemaApiProtocol<JsonRpcSchemaApiProtocol>();
app.UseSchemaApis(enableAppDataApi: true, enableSchemaManage: true);
```

See [AppService.cs](../../../SchemaNode.App/Service/AppService.cs#L18-L64) and [Service.cs](../../../SchemaNode.App/Http/Service.cs#L33-L130).

### 6.2 An Important Extension Principle

Extending the App dialect is not about "adding a hook"; it is about "adding a semantic declaration".

For example, adding a new workflow node:

```csharp
[Meta<SchemaType>($"{NS_SYSTEM_WORKFLOW_CONTROL}.my_node")]
[Meta<OfNodeKind>(NODE_KIND_WORKFLOW)]
public class MyNode: BaseWorkflow
{
    public async Task ProcessAsync(WorkflowContext context, string arg)
    {
        ...
        SetPayload(context, result);
    }
}
```

No editor changes, no route registration, no configuration. `WorkflowGenerator` generates it into a `node.workflow` type, and the editor renders it by the general rules.

This is exactly what chapter 01 said: **execution capability itself can become semantics.**

---

## 7. End-to-End Review

Linking the ten chapters together, a complete request roughly flows like this:

```text
HTTP
  ↓
SchemaApi (parse, validate, time zone/locale)
  ↓
SchemaContext
  ├── AuthorizeAsync (Auths / PolicyScope)
  ├── GetAppTypeAsync (AppType / AppFieldType)
  └── PrepareFieldDataAsync (DynamicTableSchema)
        ↓
IAppDataProvider
  ├── Query  ← AppSchemaDataFilter (front end + RowAuths)
  ├── Save   ← PushAppData / derived push
  └── Delete
        ↓
Commit
  ├── ProcessDataPush (DataDerive / observers)
  ├── Commit
  └── RaiseEvent
        ↓
Event → WaitEvent → WorkflowContext → node → Function
```

And the definition-side loop is:

```text
Edit / import
  ↓
IAppEntryStorageProvider
  ↓
AppSchemaRuntime
  ↓
AppType / AppFieldType / AppWorkflowType
  ↓
Projection (GetSchemaAsync) → editor
```

---

## 8. Closing

The value of the App dialect is not how many APIs it provides, but that it turns most of the mutable parts of an ordinary application — data model, permissions, views, derivation, processes, events — into describable, composable semantic structures.

```text
Core        → common semantics and definition domain
App         → an executable dialect
Business    → composing these capabilities
Execution   → Provider / Scheduler / Dispatcher
```

It is neither the only implementation nor the final form. Once you understand how this code projects "semantics" into "execution", you can build your own dialect in the same way, or replace any of these layers.

> The code is the current implementation of this design, not its boundary. What is truly stable is the structure of "define → load → execute", and the minimal contracts between layers.
