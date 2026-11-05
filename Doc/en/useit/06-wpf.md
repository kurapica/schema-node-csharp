# 06. WPF Host Integration

This chapter is aimed at the scenario "connecting a set of Schema views inside your own WPF application". The implementation is still split into two layers: `SchemaNode.WPF.View` depends only on Core and is responsible for rendering a `DataNode` into editable controls; `SchemaNode.WPF.Man` is a runnable Prism-based management host that demonstrates the complete integration. The view layer uses the "Wrapper + View in symbiosis" pattern: every wrapper is responsible for exactly one node shape and is paired with its own `UserControl` and `DataTemplate`, while recursion is handled by nested `SchemaView` instances.

This chapter proceeds in the order "project → initialization → binding → lifecycle → Prism host". Every step provides minimal code that can be copied directly.

| What you want to do | Assembly / type to use | Section |
| --- | --- | --- |
| Render forms only, no Schema API needed | `SchemaNode.WPF.View` | 1.1, 3 |
| Read and write schema locally or remotely | `SchemaNode.WPF.View` + `ISchemaApiProvider` from `SchemaNode.App` | 1.2, 2 |
| Need a navigable management host skeleton | `SchemaNode.WPF.Man` (Prism host example) | 1.3, 5 |

---

## 1. Projects and Dependencies

### 1.1 Render Only: Reference SchemaNode.WPF.View

`SchemaNode.WPF.View` is an ordinary WPF class library. It does not depend on Prism and never accesses any backend by itself. The view layer does exactly two things: wrap a `DataNode` into a `DataNodeWrapper`, then pick the paired `UserControl` and `DataTemplate` for the node shape. The host only needs one project reference:

```xml
<ProjectReference Include="..\SchemaNode.WPF.View\SchemaNode.WPF.View.csproj" />
```

Target framework and prerequisites:

| Item | Requirement |
| --- | --- |
| Target framework | `net10.0-windows` |
| WPF | `UseWPF=true` |
| Dependencies | Only `SchemaNode.Core`; no App layer and no Prism |

See [SchemaNode.WPF.View.csproj](../../../SchemaNode.WPF.View/SchemaNode.WPF.View.csproj). The host decides where the `DataNode` comes from and when it is created; when a bare `DataNode` is handed to `SchemaView`, wrapping and disposal become its responsibility (see 4.1).

### 1.2 Need Schema Data: Reference SchemaNode.App

Beyond rendering, reading and writing type definitions and app definitions goes through [ISchemaApiProvider.cs](../../../SchemaNode.App/Api/Client/ISchemaApiProvider.cs). It provides two implementations with different lifetimes:

| Implementation | Lifetime | Purpose |
| --- | --- | --- |
| `LocalSchemaApiProvider` | One per screen, constructed with that screen's `SchemaContext` | Read and write in-process, without HTTP |
| `RemoteSchemaApiProvider` | Stateless, can be shared globally | Access the server over HTTP |

The local implementation reuses the screen context directly, so `SetAccess(app, target)` inside `CallFunctionAsync` is written only on that screen and does not pollute other screens; the context `Locale` is also synchronized by `SchemaContextFactory`, so newly generated error and validation text follows a language switch. The remote implementation only holds an `HttpClient` and `SchemaApiProviderOptions.BaseAddress`.

Project reference:

```xml
<ProjectReference Include="..\SchemaNode.App\SchemaNode.App.csproj" />
```

### 1.3 Prism Host: Refer to SchemaNode.WPF.Man

`SchemaNode.WPF.Man` is already a runnable Prism host (`WinExe`), containing regions, navigation, seven screens, and the registration code: type definition, type definition document, type demo, app definition, app demo, workflow configuration, and function definition. When integrating into a new application, it is recommended to treat it as a template and copy the relevant files, rather than referencing an executable project directly from a business project.

Its packages and projects:

```xml
<PackageReference Include="Prism.DryIoc" Version="9.0.537" />

<ProjectReference Include="..\SchemaNode.Core\SchemaNode.Core.csproj" />
<ProjectReference Include="..\SchemaNode.App\SchemaNode.App.csproj" />
<ProjectReference Include="..\SchemaNode.WPF.View\SchemaNode.WPF.View.csproj" />
```

See [SchemaNode.WPF.Man.csproj](../../../SchemaNode.WPF.Man/SchemaNode.WPF.Man.csproj).

---

## 2. Initialization: One SchemaContext per Screen

### 2.1 Runtime Shared, Context Isolated

`ISchemaRuntime` is a process-wide singleton; the type cache, root namespace, and app runtime all live on it. `SchemaContext` is scoped and carries full mode, access context, context items, locale, and node subscriptions. Therefore initialization only needs to build the runtime once, and each screen only adds its own scope and context.

| Object | Lifetime | Description |
| --- | --- | --- |
| `ISchemaRuntime` | Process singleton | Type cache and root namespace; initialized once at startup |
| `SchemaContext` | Scoped per screen | `FullMode`, `Access`, `ContextItems`, `Locale`, load-lock ownership |
| `DataNode` / `DataNodeWrapper` | Per screen | Data nodes, subscriptions, entry cache, and disposal ownership |

See [SchemaContextFactory.cs](../../../SchemaNode.WPF.Man/Services/SchemaContextFactory.cs).

### 2.2 Creation Order Cannot Be Reversed

Each screen creates a session when it is activated. The order is a hard constraint: **get the context first, immediately set `FullMode = true`, then hand it back to the screen to load types and create nodes**.

```csharp
public SchemaScreenSession CreateSession()
{
    IServiceScope scope = _runtimeServices.CreateScope();
    SchemaContext context = scope.ServiceProvider.GetRequiredService<SchemaContext>();

    // Turn on full mode immediately; must precede any type loading and node creation
    context.FullMode = true;
    context.Locale = _locale;

    ISchemaApiProvider api = _sharedRemote ?? new LocalSchemaApiProvider(context);
    return new SchemaScreenSession(scope, context, api, Unregister);
}
```

Only after the screen obtains the session is it allowed to run `GetNodeTypeAsync`, `Create`, and hand the node to `SchemaView`:

```csharp
public void OnNavigatedTo(NavigationContext navigationContext)
{
    Teardown();
    _session = _factory.CreateSession();
    Editor.Attach(_session);
    _ = InitializeAsync();   // Task.Run inside; never waits synchronously
}
```

### 2.3 Why FullMode Must Be Set Before Creating Nodes

`DataNode.FullMode` reads `Context.FullMode` directly. Nodes in non-full mode are "dumb nodes":

- `Subscribe` returns a static empty subscription and never pushes value changes;
- `SubscribeViolated` does not fire validation-state callbacks;
- `StructType.Create` does not attach the `DataNodeType` meta property, so struct fields are not created by type;
- The `ScalarNode` constructor returns immediately, so subscriptions for `EntrySource` / `WhiteList` / `Valid` are all skipped, and neither cascading nor hot replacement takes effect.

Therefore setting `FullMode` later does not "catch up" for previously created nodes; it only makes new nodes behave normally while old nodes remain silent forever. It must be completed before node creation.

### 2.4 Screen-Level API Provider

The local provider is constructed when the session is created, and its parameter is this screen's context:

```csharp
ISchemaApiProvider api = _sharedRemote ?? new LocalSchemaApiProvider(context);
```

The remote provider is stateless and can be created once at host startup and shared. Both modes expose the same interface to screen code, and the screen only depends on `session.Api`; when the session is disposed, `SchemaScreenSession` releases data nodes, context, and scope in reverse order.

---

## 3. Binding: Wrapper + View in Symbiosis

### 3.1 Wrapping a Node

`SchemaView` accepts either a `DataNode` or an existing `DataNodeWrapper`. The default path passes only the node: the view resolves and creates the wrapper on `Loaded`, and releases it on `Unloaded`, on rebinding, or when the node is replaced. The wrapper path is used only when the host already owns one.

```csharp
DataNode node = (DataNode)((IValueTypeAccess)type).Create(session.Context);
schemaView.Node = node;   // the view resolves and wraps it when loaded
```

```xml
<view:SchemaView Node="{Binding AppNode}" Registry="{Binding Registry}" />
```

See [SchemaView.xaml.cs](../../../SchemaNode.WPF.View/SchemaView.xaml.cs), [NodeWrapperFactory.cs](../../../SchemaNode.WPF.View/NodeWrapper/NodeWrapperFactory.cs), [SchemaViewRegistry.cs](../../../SchemaNode.WPF.View/SchemaViewRegistry.cs), and [SchemaTemplates.xaml](../../../SchemaNode.WPF.View/Templates/SchemaTemplates.xaml). The wrapper owns binding, thread hand-off, errors, and disposal; the view owns layout and interaction. They correspond one to one at materialization time.

### 3.2 Each Wrapper Has a Paired View

Every concrete wrapper has its own `UserControl` and `DataTemplate` resource key:

| Wrapper | UserControl | Template key | Shape |
| --- | --- | --- | --- |
| `InputWrapper` | `InputView.xaml` | `SchemaInput` | Single value, cascading drop-downs, multi-value popup |
| `DateWrapper` | `DateView.xaml` | `SchemaDate` | `DatePicker` |
| `BoolWrapper` | `BoolView.xaml` | `SchemaBoolean` | `CheckBox` |
| `StructWrapper` | `StructView.xaml` | `SchemaStruct` | Label + recursive `SchemaView` |
| `ArrayWrapper` | `ArrayView.xaml` | `SchemaArray` | List with add/remove |
| `TableWrapper` | `TableView.xaml` | `SchemaTable` | Struct-array table |
| `FallbackWrapper` | Inline read-only `TextBox` | `SchemaFallback` | Unknown node shape |

`DataNodeWrapper` is the common base class: `INotifyPropertyChanged`, `INotifyDataErrorInfo`, dispatcher hand-off, exactly-once unsubscription, and the permission states (`Require`, `ReadOnly`, `Disable`, `DisplayOnly`) with their derived `IsEditable` / `IsRequired` / `IsOptional`. `InputWrapper` adds everything entry-related: candidates, cascading levels, multi-value state and `AsSuggest` / `LeafOnly`. Date and Bool inherit `DataNodeWrapper` directly; they need the permission states but never carry an entry source. All wrappers share the same `Registry`, `Role`, and `FieldName`.

### 3.3 Dispatch by Node Shape

`NodeWrapperFactory` first asks the registry for a custom wrapper factory; when none is declared it falls back to a fixed shape order (hosts must not write their own branches based on `Kind`):

1. `EnumArrayNode` → `InputWrapper` (multi-value popup);
2. `ArrayNode` → `TableWrapper` when `Registry.ShouldUseTable` is true, otherwise `ArrayWrapper`;
3. List nodes that implement `IEnumerable<IValueAccess>` but are not `ArrayNode` (for example system function arguments) → `ArrayWrapper`;
4. `StructNode` → `StructWrapper`;
5. `DateNode` → `DateWrapper`;
6. `BoolNode` → `BoolWrapper`;
7. `ScalarNode` → `InputWrapper` (with no candidates it degrades to the plain text editor);
8. Any other shape → `FallbackWrapper`.

Core resolves entry sources asynchronously with debounce. `InputWrapper` starts a cancellable load whenever `EntrySourceVersion` changes, so a namespace or type cascade upgrades from plain text to a drop-down as soon as the snapshot arrives.

### 3.4 Candidate Entries: Single Level, Cascade, and Multi-Value

`InputWrapper` exposes `Entries`, `Levels`, `MultiEntries`, and `HasEntries`, plus three mutually exclusive display branches:

```csharp
bool multi = wrapper.IsMulti;          // EnumArrayNode: checkbox popup
bool cascading = wrapper.IsCascading;  // multi-level entry source: one drop-down per level
bool plain = wrapper.IsPlainText;      // no candidates at all: plain TextBox
```

The cascade depth is decided by Core's entry source, so `IsCascading` is evaluated when a snapshot is applied rather than fixed at construction. Every `EntrySourceVersion` change cancels the previous load and schedules a new one; results are written into the collections in one bulk update on the dispatcher. Writes from `SelectedEntry`, `Selected`, and `IsSelected` all go through the same `Value` setter.

### 3.5 Template Resolution and Registration

Resolution takes an `IValueAccess` node directly and returns a `SchemaViewRegistration` (template key plus an optional wrapper factory); it never creates a wrapper just to resolve. The order is fixed:

1. Explicit host override: when an existing wrapper is passed in, its `TemplateKeyOverride` is read;
2. Type registration: the type itself first, then recursion along `ScalarType.BaseType`; a non-`Field` role falls back to `Field`, and a non-`default` skin falls back to `default`;
3. Kind registration: built-in bool / int / decimal / string / enum / object / struct / date; arrays go through `ResolveArray`, which picks table or list via `ShouldUseTable`;
4. Default view for the node shape: known shapes map to their view, scalars end at `SchemaInput`, and unknown shapes degrade to the read-only `SchemaFallback`.

`SchemaViewRegistry` exposes three registration entry points: `RegisterKind` binds a node shape, `RegisterType` overrides a type (optionally with a role, skin, and `asSingle`), and `RegisterTable` is the shorthand for "render a struct-backed array as a table". All three accept an optional `wrapperFactory: Func<IValueAccess, DataNodeWrapper>`; when omitted, the built-in wrapper for that shape (Input / Date / Bool / Struct / Array / Table / Fallback) is used. Registering the same type twice replaces the previous entry, which is exactly how a host overrides a built-in view.

```csharp
SchemaViewRegistry registry = SchemaViewRegistry.CreateDefault();
SchemaViewRegistration reg = registry.Resolve(node, SchemaViewRole.Field, "default");
DataNodeWrapper? custom = reg.WrapperFactory?.Invoke(node);

registry.RegisterKind(Constant.SCHEMA_KIND_NODE_OBJECT, SchemaTemplateKeys.Struct);
registry.RegisterType("system.identifier", "SchemaImmutableTextCell", SchemaViewRole.Cell,
    wrapperFactory: n => new IdentifierWrapper((DataNode)n));
registry.RegisterTable("system.schema.struct.fields");
```

Type-registration lookups are cached per `(type, role, skin)` and invalidated when the registry changes; registrations are cached, never wrapper instances, so the same node rendered in different views still gets an independent wrapper. After a resource key is resolved, `SchemaTemplateSelector` first looks for host resources reachable from the container, then for the built-in `Templates/SchemaTemplates.xaml`; a host can therefore override any key or register a brand-new one without modifying the library. `Role=Cell` is the compact table-cell form and does not render the inline error block.

### 3.6 Visibility, Validation, and Localization

`SchemaView` has only three dependency properties: `Node` (accepts a `DataNode` or a `DataNodeWrapper`), `Role` (`SchemaViewRole`), and `Skin` (a string). `IsVisible` is not a fourth property: `DataNodeWrapper.IsVisible` is bound one-way to the internal `ContentPresenter` `Visibility`, so Core `Visible` / `InVisible` / `Attach` changes appear directly on screen. `Role` and `Skin` are forwarded to the nearest `SchemaView` and take part in template resolution.

When a write through the `Value` setter fails, the reason is not swallowed: it surfaces `DataNode.Error` first, and when absent gives a precise conversion-failure message; `GetErrors` returns the same real error, falling back to the names of the constraints actually violated.

```xml
<view:SchemaView Node="{Binding Node}" Role="Field" Skin="default" />
<TextBox Text="{Binding Value, Mode=TwoWay, UpdateSourceTrigger=LostFocus,
                ValidatesOnNotifyDataErrors=True}" />
```

All text goes through the `LocaleManager.Instance` `Version` binding: `Content="{Binding Source={x:Static loc:LocaleManager.Instance}, Path=Version, Converter={x:Static loc:LocalizedTextConverter.Instance}, ConverterParameter=wpf.button.new}"`. When the language changes, `Version` increments and every XAML label refreshes in place; the wrapper's `Label` / `Error` and table column headers are recomputed on the same event without rebuilding the visual tree. Keys in `zhCN.json` and `enUS.json` must exist as pairs; a missing key falls back to the key itself.

---

## 4. Lifecycle

### 4.1 Wrapper Ownership and Reverse Disposal

`SchemaView` accepts two kinds of context with different ownership rules. When the host passes a bare `DataNode`, the view creates the wrapper through the registry at `Loaded` materialization and owns it (`_owned`), releasing it on `Unloaded`, on rebinding, on a `Role` / `Registry` change, or when the node is replaced. When the host passes an existing `DataNodeWrapper`, the wrapper is created and disposed by the host, and `SchemaView` only removes its own binding. `_pendingNode` lets an unloaded view remember the node without wrapping it early; the `_hasContent` guard makes repeated `Loaded` / `DataContextChanged` callbacks return immediately instead of rebuilding the whole subtree.

When a screen closes, destruction is strictly in reverse order: **dispose all wrappers first (unsubscribing), then destroy the DataNodes, and finally destroy the `SchemaContext` and the scope**.

```csharp
public void Teardown()
{
    Editor.Dispose();          // Wrapper tree unsubscribes first
    _session?.Dispose();       // Remaining DataNodes → Context → Scope
}
```

The fallback order inside the session:

```csharp
for (int i = _nodes.Count - 1; i >= 0; i--)
    _nodes[i].Dispose();
Context.Dispose();
_scope.Dispose();
```

`DataNode.Dispose()` is synchronous, and `StructNode` / `ArrayNode` recursively release child nodes; `SchemaContext.Dispose()` is also synchronous and only clears context items. A session removes itself from the `SchemaContextFactory` live list before disposal, so a language switch never reaches a dead context.

### 4.2 Array and Table Rebinding

`StructWrapper` and `TableWrapper` reconcile children by node identity: a value change keeps the same wrapper instances, and only schema changes (for example an `Attach`-driven field-set switch) add or drop entries. Table columns are derived once from the element `StructType` fields, skipping fields marked `InVisible`. When the host maintains its own array, rebinding must unsubscribe before clearing:

```csharp
foreach (DataNodeWrapper child in Children)
    child.Dispose();
Children.Clear();
// Then rebuild wrappers for the new elements
```

Old wrappers then stop receiving events, and recycled virtualization containers never touch a disposed subscription.

### 4.3 Invalidation Rebuild After Saving

Saving a type sets the corresponding type's `Loaded=false`, and the old `DataNode` becomes a dead reference. Do not keep reusing it; perform an invalidation rebuild within the current screen:

```csharp
await Editor.ReloadSchemaAsync(_fullName, reload: true);
```

`ReloadSchemaAsync` first disposes the old wrapper tree and old DataNode, then reads the latest definition through `GetSchemaAsync`, resolves the type with `reload: true`, and creates a new node. The rebuild affects only the current screen; other screens are unaffected. After a successful save, the type document screen switches to `edit` mode and reloads immediately.

---

## 5. Prism Host Wiring

### 5.1 Region and Navigation

Declare the region in the shell; navigation targets are injected here:

```xml
<ContentControl prism:RegionManager.RegionName="MainContentRegion" />
```

Navigate to the default screen after startup:

```csharp
Container.Resolve<IRegionManager>().RequestNavigate(
    "MainContentRegion", nameof(TypeListView),
    result => { if (!result.Success) Report(result.Exception); });
```

See [MainWindow.xaml](../../../SchemaNode.WPF.Man/MainWindow.xaml) and [App.xaml.cs](../../../SchemaNode.WPF.Man/App.xaml.cs). `CreateShell` explicitly resolves the shell `DataContext` instead of relying on Prism's `.Views` namespace convention; startup navigation runs asynchronously after `OnInitialized`, and failures are written to the shell `StatusMessage` instead of leaving an empty region silently.

### 5.2 RegisterTypes

Registration has three steps: build the runtime, register the screen-level factory and view registry, and register views and ViewModels.

```csharp
services.AddAppSchemaAssembly<App>();
services.AddSchemaStorageProvider<DynamicAppEntryStorageProvider>();
services.AddAppDataProvider<InMemoryAppDataProvider>();
services.PrepareSchemaRuntime();
_schemaServices = services.BuildServiceProvider();

ISchemaApiProvider? sharedRemote = CreateSharedRemote();
containerRegistry.RegisterInstance(new SchemaContextFactory(_schemaServices, sharedRemote));
containerRegistry.RegisterInstance(ManSchemaViews.CreateRegistry());
containerRegistry.Register<TypeListViewModel>();
containerRegistry.RegisterForNavigation<TypeListView>();
containerRegistry.RegisterForNavigation<TypeDocumentView>();
```

ViewModels are registered as transient (`Register<T>()` is transient by default), so every navigation gets a new instance; do not register them as singletons, otherwise they conflict with the screen-level context. `ManSchemaViews.CreateRegistry()` adds tables for struct fields, relations, enum values, and app fields on top of the built-in defaults, and assigns a read-only cell template to `system.identifier`. Runtime initialization (`InitSchemaRuntimeAsync`) likewise runs asynchronously after `OnInitialized` and is never awaited synchronously inside `RegisterTypes`.

Navigation buttons use `DelegateCommand` and await `INavigationResult`:

```csharp
public sealed class MainWindowViewModel
{
    public MainWindowViewModel(IRegionManager regionManager)
    {
        NavigateCommand = new DelegateCommand<string>(
            view => Navigate(view));
    }

    public DelegateCommand<string> NavigateCommand { get; }
}
```

### 5.3 Screen ViewModel and the Type Definition Screen

Screen ViewModels implement `INavigationAware` and `IDestructible`, and create the session only when navigation activates:

```csharp
public void OnNavigatedTo(NavigationContext navigationContext)
{
    Teardown();
    _session = _factory.CreateSession();
    _ = InitializeAsync(navigationContext);
}
```

The namespace control in [TypeListView.xaml](../../../SchemaNode.WPF.Man/Views/TypeListView.xaml) is itself a `SchemaView` over a `system.schema.namespace.type` node, whose Core entry source supplies child namespaces level by level; selecting a concrete type writes the node back so the selector stays aligned with the list. This is the only place where a ViewModel deliberately owns a root wrapper: the VM has to observe `InputWrapper.Value` to drive the cascade and write the selection back. The list is a read-only name / display / kind table plus an action column; its rows are `NodeSchema`, not `DataNode`, so it uses a native `ListView + GridView` rather than `TableView`. `TableView` is still used for struct-backed arrays (struct fields, relations, enum values, app fields), registered through `RegisterTable` and driven by `TableWrapper`.

[TypeDocumentView.xaml](../../../SchemaNode.WPF.Man/Views/TypeDocumentView.xaml) carries the view, edit, and new flows through the route parameters `mode` / `name` / `namespace`: `IsEditable=false` for view, editable for edit and new. The content is rendered by [SchemaEditorView.xaml](../../../SchemaNode.WPF.Man/Views/SchemaEditorView.xaml) over the `system.schema.node.schema` definition node, with `HideNameBar` hiding the name bar that already exists on the list screen. Saving runs through `Task.Run(SaveSchemaAsync)` and reloads on success; the back button returns to `TypeListView` with the `namespace` parameter, so the list stays in the original namespace.

Apart from the namespace control, ViewModels pass nodes only and never call `NodeWrapperFactory.Create`:

| Screen | Creates a wrapper up-front? |
| --- | --- |
| Type definition | Yes, namespace control only; the VM observes `InputWrapper.Value` for the cascade and write-back |
| Type definition document | No; `SchemaEditorViewModel` only holds `Node` |
| Type demo | No; the editor only holds `Node` |
| App definition | No; `AppNode` and `FieldsNode` are nodes |
| App demo | No; it does not use SchemaView |
| Workflow configuration | No; the property panel binds the selected node's `Node` directly |
| Function definition | No; arguments, result, and editor all pass nodes |

Both `OnNavigatedFrom` and `Destroy` call `Teardown`, ensuring repeated callbacks are also safe.

### 5.4 Avoiding Navigation Caching of the Old Context

Prism's region keeps views by default. Four measures must be taken together to keep the context aligned with the screen lifetime:

| Mechanism | Effect |
| --- | --- |
| `[RegionMemberLifetime(KeepAlive = false)]` | Removes the View when leaving the region; no reuse |
| `IsNavigationTarget => false` | Navigation requests are not routed to old instances |
| `Register<T>()` transient ViewModel | Every navigation gets a new ViewModel |
| `OnNavigatedFrom` / `Destroy` → `Teardown` | Old context is guaranteed to be destroyed |

See the code-behind files of the screens, for example [TypeListView.xaml.cs](../../../SchemaNode.WPF.Man/Views/TypeListView.xaml.cs).
