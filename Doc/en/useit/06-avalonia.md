# 06. Avalonia Integration and Usage

> This chapter mirrors the structure of [06-wpf.md](06-wpf.md) one-to-one. Both platforms share Core and the wrapper semantics; the differences come only from control capabilities and the host stack. Section 6 lists every actual difference.

`SchemaNode.Avalonia.View` renders the schema tree into Avalonia controls and provides tables, cascade editing, localization, and the debug panel. It depends only on `SchemaNode.Core`—no host, navigation framework, or Prism.

## 1. Projects and Dependencies

### 1.1 Render-only: `SchemaNode.Avalonia.View`

- Targets `net10.0`, references `Avalonia` 12.1.5 and `SchemaNode.Core`, and has no Prism or other MVVM framework dependency.
- `SchemaView : Grid` is the recursive dispatch control: the host supplies a `DataNode` / `DataNodeWrapper` in `Node` (or `DataContext`), the registry chooses a view, and that view renders children through nested `SchemaView`s.
- Avalonia has no WPF-style `DataTemplateSelector`, so `SchemaViewTemplates` instantiates the view directly from the registry key. The presenter content is replaced only when the resolved key or the wrapper actually changes.
- `Registry`, `Role`, and `Skin` are inherited properties on `SchemaView`; `Debug` is an inherited attached property, so a host sets it once at the outer element and the whole subtree follows.

### 1.2 Schema data: `SchemaNode.App` / `AppView` / `AI`

- Paged tables live in `SchemaNode.Avalonia.AppView`: `PageTableWrapper` derives from `TableWrapper` and reads pages through `IPageTableAdapter`.
- `AppView` ships its own language file (14 keys). This differs from WPF.AppView, which has no separate language file.

### 1.3 Host: `SchemaNode.Avalonia.Man`

- A `net10.0` executable referencing `Avalonia.Desktop`, `Avalonia.Themes.Fluent`, and Core / App / AppView / AI / View.
- No Prism: navigation and MVVM come from minimal implementations under `Navigation/` (`IRegionManager`, `RegionManager`, `ManViewFactory`, `NavigationContext`, `INavigationAware`, `IDestructible`, `BindableBase`, `DelegateCommand`).
- The theme is pinned to Light, matching the WPF host.

## 2. Initialization

### 2.1 Initialize the process-wide runtime once

`ManApp.BuildServices()` builds the `ServiceProvider`:

1. `AddAppSchemaAssembly<ManApp>()`, `AddSchemaStorageProvider<...>()`, `AddAppDataProvider<...>()`, `AddSchemaChat(...)`, then `PrepareSchemaRuntime()`.
2. Register the host registry `ManSchemaViews.CreateRegistry()`, `SchemaContextFactory`, `ManViewFactory`, `RegionManager` / `IRegionManager`, `DialogService`, `LanguageSettingsStore`, and `LanguageService`.
3. Every screen view model is `AddTransient`.

In `ManApp.OnFrameworkInitializationCompleted`:

1. `Locale.MergeAssembly(typeof(ManApp).Assembly)` merges the Man language files;
2. create `MainWindow` with `MainWindowViewModel` as its DataContext;
3. under `#if DEBUG`, call `SchemaView.SetDebug(shell, true)` so the whole window inherits the inspector;
4. `StartAsync()`: `InitSchemaRuntimeAsync()` → `LanguageService.InitializeAsync()` (restores the persisted locale) → navigate to `TypeListView`.

The runtime is a process-wide singleton; no screen calls `InitSchemaRuntimeAsync` again.

### 2.2 One `SchemaContext` per screen

`SchemaContextFactory.CreateSession()` has a fixed order:

1. `CreateScope()` from the root container and resolve the scoped `SchemaContext`;
2. set `context.FullMode = true` **immediately**—`DataNode.FullMode` reads `Context.FullMode` directly, so this must happen before loading types or creating nodes;
3. add it to the live set and apply the persisted locale;
4. choose the API provider: remote mode reuses the stateless shared `RemoteSchemaApiProvider`; local mode creates a `LocalSchemaApiProvider` bound to that screen's scoped context;
5. resolve `IAIChat` / `ISchemaFeatures` from the screen scope and return a `SchemaScreenSession`.

### 2.3 First screen

The navigation callback in `StartAsync` writes failures to `MainWindowViewModel.StatusMessage`, so the process never stops silently on an empty window.

## 3. Binding

### 3.1 Who creates the wrapper

- Host-supplied wrapper: `SchemaView` only observes it and never disposes it when leaving the tree or rebinding.
- Bare `DataNode`: `SchemaView` creates a wrapper with the current `Registry` when it enters the tree and releases its own wrapper when it leaves or rebinds.
- Repeated items such as table rows and array elements rebuild their wrappers when `DataContext` changes; the rebuild detaches the old wrapper's events before attaching the new one.

### 3.2 Wrapper / view pairs

| Wrapper | View | Notes |
| --- | --- | --- |
| `StructWrapper` | `StructView` | Field rows with a label column and an editor column |
| `ArrayWrapper` | `ArrayView` | Row list plus add/delete |
| `TableWrapper` | `TableView` | Columns from `StructType` fields |
| `InputWrapper` | `InputView` | Text, drop-down, multi-select, cascade, suggest |
| `BoolWrapper` | `BoolView` | Boolean |
| `DateWrapper` / `YearWrapper` | `DateView` / `YearView` | Date and year |
| `RangeDateWrapper` | `RangeDateView` | Range date, branching on year/month/day |
| `LanguageWrapper` | `LanguageView` | Dedicated `system.language` view |
| `LocaleStringWrapper` | `LocaleStringView` | `system.localestring` and the translation dialog |
| `FlagsEnumWrapper` | `FlagsEnumView` | Enum flags, with a single-select drop-down when not multi |
| `TypeSelectorWrapper` | `TypeSelectorView` | Type path plus generic argument slots |
| `FallbackWrapper` | `FallbackView` | Unregistered types |

### 3.3 Shape dispatch

`NodeWrapperFactory` chooses a wrapper from the node implementation (StructNode / ArrayNode / ScalarNode / AnyNode) and `Role`. The role decides whether a label is rendered, whether errors appear, and whether editing is allowed, exactly as in WPF.

### 3.4 Candidates and cascade

- Single-level candidates come from `ScalarNodeImpl`. When `IsEditable` is true, free text is allowed.
- `system.language` uses the dedicated `LanguageView` instead of a generic drop-down.
- Cascade path and expansion rules are covered in 3.7.

### 3.5 Template selection

`SchemaViewRegistry.Resolve(node, Role, Skin)` returns a `TemplateKey`; `SchemaViewTemplates.Create(key, wrapper)` instantiates the matching view and falls back to `FallbackView`. A host can replace a key with `SchemaViewTemplates.Set(...)` or register a type-specific template with `RegisterType(...)`; the Man host uses both to override function call arguments and function definitions.

### 3.6 Visibility, validation, localization

- `IsVisible` and `IsEditable` are computed by the wrapper; views only bind to them.
- Validation errors come from Core's `Constraint` / `Error`; write failures additionally produce `VALUE_NOT_VALID`.
- Text binds through `LocaleManager.Instance.Version` and `LocalizedTextConverter`, so language switches refresh in place without rebuilding the visual tree.

### 3.7 Input controls, cascade, and validation

Sizing is constrained at the template layer: `SchemaViewTemplates` applies `MinWidth = 120` and horizontal stretch to Input / Boolean / Date / Array / TypeSelector / FlagsEnum / Year / Language / RangeDate / LocaleString, and `InputView` sets `MinHeight="24"`.

- `TypeSelectorView.InputMinWidth` defaults to 120; generic slots explicitly pass `MinWidth="0" InputMinWidth="0"`. The path column is one `star` and the generic segment gets n `star` columns for n arguments, so the path and each argument take roughly 1/(n+1). Without generics the generic column is 0.
- Table cells use a natural width of 180 and a minimum of 140; locale columns use 200; the operation column is 100.

Cascade (`CascaderView` + `InputWrapper`):

- Clicking the field toggles the popup; the field shows `CascadeSummary`, joining levels with `" / "`.
- Each candidate row has two hit areas: the label calls `SelectLevelEntry` (writes and closes when committable), and the right arrow calls `ExpandLevelEntry` (loads and expands the next level only).
- Non-leaf nodes are not committable by default: with `LeafOnly` or a type that disallows parent selection, clicking the label only expands. Disabled entries ignore clicks. `IsNonLeafNodeSelectable` decides whether a parent may be committed.
- Closing without committing calls `ReloadCascadeSelection()` to restore the committed path and discard temporary parent picks.
- The clear button appears only when `CanClear = IsEditable && !Require && HasValue`; a `Require` field has no clear entry.
- The loading state shows only while cascading, loading, and still without candidates (`ShowCascadeLoading`), using `avalonia.page.loading`.
- The popup anchors to the input's bottom-left corner: `Popup Placement="BottomEdgeAlignedLeft"` with the whole field row as the target. Switching levels widens it only to the right; the left edge never moves.

Validation and errors:

- Inline errors attach only to value-rendering leaves: `ShowInlineError = RendersValue && Role == Field`. Struct / Array / Table containers do not show them; table cells are marked through `ShowCellError`.
- When the `Value` write fails, the original text stays in `_pendingText` and `ReadEditorValue` prefers the draft. The message prefers `DataNode.Error`, then `VALUE_NOT_VALID`, then `avalonia.view.error.invalid`.
- The next successful write clears both the draft and the error; an external value change drops the draft and shows the current node value.
- Errors are exposed both as an inline `TextBlock` and as `ToolTip.Tip=ErrorTip` (only when not inline).

### 3.8 Tables

Columns and `ViewColumns`:

- Columns derive from the array element's `StructType` fields, skipping `InVisible == true`. `Struct` fields form two-level headers; `StructArray` fields form sub-tables with their own sub-operation column.
- `SchemaTableOptions.ViewColumns` is a read-only column whitelist that accepts nested paths such as `discount.rate`. When non-empty it keeps only matching columns and enters `RowEditorMode`: cells are read-only and changes go through the operation column.
- `ReadOnly` disables cell editing plus add/delete, but the row-editor entry enabled by `ViewColumns` stays available.

Widths:

- `TableView` takes its budget from the nearest ancestor `ScrollViewer` viewport and, like WPF, pins its own `Width` to that budget. `TableWrapper.AvailableWidth` drives `TableColumnLayout.Distribute`: fill by default, grow proportionally to natural widths, shrink to the minimums when tight, then scroll only the body.
- Header and body share one `EffectiveColumnWidths` snapshot instead of recomputing separately.

Row editing:

- The entry is in the operation column. The dialog's initial size is 2/3 of its owner through `TableRowEditorWindowLayout` (minimum 360×240, 720×560 without an owner) and it is centered.
- Adding inserts a real element first, then opens the editor. Cancelling removes that provisional row; a validation failure on confirm keeps the dialog open with `avalonia.table.error.invalid`.
- Editing an existing row modifies the original element after taking a field snapshot and restores the snapshot on cancel.
- Outside row-editor mode, adding only inserts an empty row for direct cell editing.

Paged tables (`PageTableWrapper` in `SchemaNode.Avalonia.AppView`):

- The host adapter supplies the page size. Switching pages captures the current page's edits, loads the target page, and reapplies them.
- Cross-page edits are tracked by primary key and only when the key can be determined; deleted rows are recorded by key and stay hidden when the page is revisited.
- Paged tables do not support adding rows; the add entry shows a hint. `AcceptChanges` clears the tracker.
- If the total row count shrinks below the current page, the page clamps to the last one.
- Deactivating the screen releases the current page and cancels in-flight loads; reactivating reloads it. Changing filters returns to the first page.

### 3.9 Localization

- The dedicated `LanguageView` for `system.language` uses a closed drop-down of configured locales with no empty option; selecting one writes the value back.
- A `system.localestring` field row shows the current value and opens the translation dialog from its button; the dialog edits one row per language and supports add, delete, confirm, and cancel.
- The dialog opens with no default row and never preselects a language for a new row; existing rows are sorted by language.
- Confirming moves focus out of the text box first to commit the text being edited; a failed write keeps the dialog open with `avalonia.locale.translations.failed`.
- Key ownership: `SchemaNode.Avalonia.View/locale` has 30 view keys, `SchemaNode.Avalonia.Man/locale` has 349 screen keys, and `SchemaNode.Avalonia.AppView/locale` has 14 paged-table keys. All use the `avalonia.` prefix and never share keys with WPF's `wpf.`.

### 3.10 Debug Panel

- `SchemaView.Debug` is an inherited attached property (`SchemaView.SetDebug(control, true)`). When on, `SchemaView` appends a 🐞 trigger to the right of its content and creates the popup on demand; turning it off removes both from the visual tree.
- The popup body comes from `SchemaDebug.Describe(node)` and always has three sections: Properties (name / value / source), Relations (property / mode / data / source), and Errors (access path / constraint / message).
- The panel is 320–720 wide with a 420 maximum height; long content scrolls inside the panel.
- The row-editor window inherits the host's `Debug` value through `TableRowEditorWindowLayout`, so its editors expose the same debug entry.

## 4. Lifecycle

- Wrapper ownership matches WPF: host-supplied wrappers are observed but never disposed; bare nodes get a wrapper created by `SchemaView` and released when it leaves the tree or rebinds.
- Screen view models implement `INavigationAware` + `IDestructible`; both `OnNavigatedFrom` and `Destroy` call `Teardown()`, so repeated callbacks are safe.
- `Teardown()` disposes wrappers the screen created (such as the namespace and kind controls), then calls `_session.Dispose()`. `SchemaScreenSession.Dispose` releases wrapper → node → context → scope and removes itself from the factory's live set.
- `RegionManager` finishes navigation in the WPF order: `OnNavigatedFrom` → `Destroy` → clear the old view's `DataContext`, which detaches nested `SchemaView`s and releases their self-created wrappers.

## 5. Host Wiring (without Prism)

### 5.1 DI

`ManApp.BuildServices()` builds the container with `Microsoft.Extensions.DependencyInjection` (see 2.1). There is no Prism container and no `ViewModelLocator` convention; every view model is explicitly registered as Transient.

### 5.2 Navigation

`RegionManager` owns one current content area only; every navigation passes the constant `MainContentRegion`, but the `regionName` value does not participate in routing:

- `RequestNavigate(regionName, viewName, callback, parameters)` resolves the view model from DI, creates the view through `ManViewFactory`, sets `DataContext`, and calls `OnNavigatedTo`;
- then calls `OnNavigatedFrom` / `Destroy` on the previous view model and clears the old view's `DataContext`;
- raises `CurrentViewChanged` so the shell swaps its content;
- `ManViewFactory` throws “No view is registered” for an unknown route; the exception is written to the `Debug` output and returned through the navigation callback's `NavigationResult`.

`ManViewFactory` uses an explicit map (`nameof(TypeListView) → (TypeListViewModel, new TypeListView())`) instead of a naming convention.

### 5.3 Screens

There are 12 navigation screens: `TypeListView`, `TypeDocumentView`, `TypeDemoView`, `AppListView`, `AppDefinitionView`, `AppDemoView`, `AppFieldListView`, `AppFieldDefinitionView`, `AppFieldDemoView`, `AppWorkflowListView`, `FunctionDefinitionView`, and `WorkflowConfigView`. The shell exposes only `TypeListView` and `AppListView` directly; the rest navigate through `IRegionManager` from within screens.

### 5.4 Language switching

`MainWindowViewModel.SwitchLanguage()` calls `LanguageService.SwitchAsync(locale)`:

1. `SchemaContextFactory.ApplyLocale` pushes the new locale to every live context first;
2. `LocaleManager.Instance.SetLocale` refreshes UI bindings;
3. `LanguageSettingsStore` persists it; a storage failure does not undo the switch.

### 5.5 View registration

`ManSchemaViews.CreateRegistry()` registers tables for struct fields, relations, enum values, and app fields, and overrides function call arguments, function definitions, and similar templates through `SchemaViewTemplates.Set` / `RegisterType`. New screens can add routes through `ManViewFactory.Register(...)` without touching the core map.

## 6. Differences from the WPF Version

Both platforms share Core and the wrapper semantics. The differences below come only from control capabilities and the host stack:

| Area | WPF | Avalonia |
| --- | --- | --- |
| Editable single-level drop-down | Native `ComboBox.IsEditable`; candidates and free text share one control | `ComboBox` is not editable, so it is split into the `IsSuggest` branch: `TextBox` + arrow button + `Popup` |
| Flags single-select form | `FlagsView` only has the check-list popup | `FlagsEnumView` adds a single-select `ComboBox` when `!IsMulti` |
| Range date | `RangeDateView` treats `system.range*` as a struct of two child nodes and places two `SchemaView`s side by side | `RangeDateView` branches on `IsYear` / `IsMonth` / `IsDate` with `NumericUpDown`, a yyyy-MM text box, or `DatePicker`, and shows start/end errors separately |
| Error channel | Inline `TextBlock` plus a control `ToolTip` bound to `Error` | Inline `TextBlock` plus `ToolTip.Tip` bound to `ErrorTip` (only when not inline) |
| Uncommitted draft | An external value refresh keeps `_pendingText` | An external value change drops the draft |
| Popup anchor | `Placement="Bottom"` is already bottom-left aligned | All popups use `Placement="BottomEdgeAlignedLeft"` |
| Row-editor dialog | Same sizing rule: 2/3 of the owner (minimum 360×240, 720×560 without an owner), clamped to the work area; always modal via `ShowDialog` | Initial size is 2/3 of the owner (minimum 360×240, 720×560 without an owner); opens non-modally and is treated as cancel without an owner; content disables horizontal scrolling |
| Debug panel implementation | `SchemaDebugPanel.xaml` + `SchemaDebug.Describe` | `SchemaDebugPanel.cs` builds the same structure in code |
| Locale keys | `wpf.` prefix, 40 view + 343 screen keys; AppView has no language file | `avalonia.` prefix, 30 view + 349 screen + 14 AppView keys |
| Host stack | Prism Region + `RegisterForNavigation` | No Prism: local `IRegionManager` + explicit View/ViewModel map + `INavigationAware` |
