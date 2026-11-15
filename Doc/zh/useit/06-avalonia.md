# 06. Avalonia 接入与使用

> 本篇与 [06-wpf.md](06-wpf.md) 的章节结构一一对应。两端共享 Core 与 Wrapper 语义，差异只来自控件能力与宿主技术栈；第 6 节集中列出两端实际存在的差异。

`SchemaNode.Avalonia.View` 把 Schema 树渲染成 Avalonia 控件，并提供表格、级联、本地化与调试面板。它只依赖 `SchemaNode.Core`，不依赖任何宿主、导航框架或 Prism。

## 1. 工程与依赖

### 1.1 只渲染：`SchemaNode.Avalonia.View`

- 目标框架 `net10.0`，引用 `Avalonia` 12.1.5 与 `SchemaNode.Core`，没有 Prism 或其他 MVVM 框架依赖。
- `SchemaView : Grid` 是递归分发控件：宿主给 `Node`（或 `DataContext`）里的 `DataNode` / `DataNodeWrapper`，注册表决定用哪个 View，View 再用嵌套 `SchemaView` 渲染子节点。
- Avalonia 没有 WPF 的 `DataTemplateSelector`，模板由 `SchemaViewTemplates` 按注册表返回的 key 直接实例化视图；只有解析出的 key 或 wrapper 真正变化时才替换 `ContentPresenter.Content`。
- `SchemaView` 的 `Registry`、`Role`、`Skin` 都是继承属性；`Debug` 是继承附加属性，宿主在最外层设置一次即可覆盖整棵子树。

### 1.2 Schema 数据：`SchemaNode.App` / `AppView` / `AI`

- 表格分页在 `SchemaNode.Avalonia.AppView`：`PageTableWrapper` 继承 `TableWrapper`，通过 `IPageTableAdapter` 读取页数据。
- `AppView` 带自己的语言文件（14 条 key），与 WPF.AppView 不同——WPF 的 AppView 没有独立语言文件。

### 1.3 宿主：`SchemaNode.Avalonia.Man`

- `net10.0` 可执行程序，引用 `Avalonia.Desktop`、`Avalonia.Themes.Fluent` 以及 Core / App / AppView / AI / View。
- 没有 Prism：导航与 MVVM 由 `Navigation/` 下的一组最小实现提供（`IRegionManager`、`RegionManager`、`ManViewFactory`、`NavigationContext`、`INavigationAware`、`IDestructible`、`BindableBase`、`DelegateCommand`）。
- 主题固定为 Light，与 WPF 宿主一致。

## 2. 初始化

### 2.1 进程级 Runtime 只初始化一次

`ManApp.BuildServices()` 构建 `ServiceProvider`：

1. `AddAppSchemaAssembly<ManApp>()`、`AddSchemaStorageProvider<...>()`、`AddAppDataProvider<...>()`、`AddSchemaChat(...)`，再 `PrepareSchemaRuntime()`。
2. 注册宿主视图注册表 `ManSchemaViews.CreateRegistry()`、`SchemaContextFactory`、`ManViewFactory`、`RegionManager` / `IRegionManager`、`DialogService`、`LanguageSettingsStore`、`LanguageService`。
3. 所有画面 ViewModel 是 `AddTransient`。

`ManApp.OnFrameworkInitializationCompleted` 里：

1. `Locale.MergeAssembly(typeof(ManApp).Assembly)` 合并 Man 的语言文件；
2. 创建 `MainWindow`，DataContext 是 `MainWindowViewModel`；
3. `#if DEBUG` 时对 Shell 执行 `SchemaView.SetDebug(shell, true)`，整窗继承调试开关；
4. `StartAsync()`：`InitSchemaRuntimeAsync()` → `LanguageService.InitializeAsync()`（读回持久化的 locale）→ 导航到 `TypeListView`。

Runtime 是进程级单例，任何画面都不会再次调用 `InitSchemaRuntimeAsync`。

### 2.2 每个画面一个 `SchemaContext`

`SchemaContextFactory.CreateSession()` 的顺序固定：

1. 从根容器 `CreateScope()`，取 scoped `SchemaContext`；
2. **立刻** `context.FullMode = true`——`DataNode.FullMode` 直接读 `Context.FullMode`，必须在加载类型或创建节点之前完成；
3. 记入 live 集合，并设置持久化 locale；
4. 选择 API Provider：远端模式复用无状态的共享 `RemoteSchemaApiProvider`，本地模式为每个画面创建使用该 scoped context 的 `LocalSchemaApiProvider`；
5. 从画面 scope 解析 `IAIChat` / `ISchemaFeatures`，返回 `SchemaScreenSession`。

### 2.3 首屏

`StartAsync` 的导航回调负责把失败写进 `MainWindowViewModel.StatusMessage`，不会让进程静默停在空窗口。

## 3. 绑定

### 3.1 Wrapper 由谁创建

- 宿主直接传 wrapper：`SchemaView` 只观察、不拥有，离开视觉树或重绑时不会 Dispose。
- 宿主传裸 `DataNode`：进入视觉树时按当前 `Registry` 创建 wrapper，离开或重绑时释放自己创建的那一个。
- 表体行、数组元素等重复项在 `DataContext` 变化时重建 wrapper；重建会先解绑旧 wrapper 的事件，再绑定新 wrapper。

### 3.2 包装器与视图成对

| Wrapper | View | 说明 |
| --- | --- | --- |
| `StructWrapper` | `StructView` | 字段逐行，标签列 + 编辑器列 |
| `ArrayWrapper` | `ArrayView` | 行列表 + 新增/删除 |
| `TableWrapper` | `TableView` | 按 `StructType` 字段生成列 |
| `InputWrapper` | `InputView` | 文本、下拉、多选、级联、建议 |
| `BoolWrapper` | `BoolView` | 布尔 |
| `DateWrapper` / `YearWrapper` | `DateView` / `YearView` | 日期、年份 |
| `RangeDateWrapper` | `RangeDateView` | 区间日期，按年/月/日分支 |
| `LanguageWrapper` | `LanguageView` | `system.language` 专用视图 |
| `LocaleStringWrapper` | `LocaleStringView` | `system.localestring` 与翻译弹窗 |
| `FlagsEnumWrapper` | `FlagsEnumView` | 枚举复选，非多选时给单选下拉 |
| `TypeSelectorWrapper` | `TypeSelectorView` | 类型路径 + 泛型参数槽 |
| `FallbackWrapper` | `FallbackView` | 未注册类型 |

### 3.3 形状分流

`NodeWrapperFactory` 根据节点实现（StructNode / ArrayNode / ScalarNode / AnyNode）和 `Role` 选择 wrapper；`Role` 会影响是否渲染标签、是否显示错误、是否允许编辑，与 WPF 相同。

### 3.4 候选与级联

- 单层候选来自 `ScalarNodeImpl` 的可选值；`IsEditable` 为真时允许自由输入。
- `system.language` 走专用 `LanguageView`，不经过普通下拉。
- 级联路径与展开规则见 3.7。

### 3.5 模板选择

`SchemaViewRegistry.Resolve(node, Role, Skin)` 返回 `TemplateKey`；`SchemaViewTemplates.Create(key, wrapper)` 实例化对应视图，未命中时用 `FallbackView`。宿主可以用 `SchemaViewTemplates.Set(...)` 替换某个 key，或 `RegisterType(...)` 为特定类型注册专属模板；Man 端用这两种方式覆盖函数调用参数、函数定义等画面。

### 3.6 可见性、校验、本地化

- `IsVisible`、`IsEditable` 由 wrapper 计算，视图只绑定。
- 校验错误来自 Core 的 `Constraint` / `Error`；输入失败另走 `VALUE_NOT_VALID`。
- 文案通过 `LocaleManager.Instance.Version` 与 `LocalizedTextConverter` 绑定，切换语言时原地刷新，不重建视觉树。

### 3.7 输入控件、级联与校验

尺寸约束在模板层：`SchemaViewTemplates` 对 Input / Boolean / Date / Array / TypeSelector / FlagsEnum / Year / Language / RangeDate / LocaleString 统一 `MinWidth = 120` 并水平拉伸；`InputView` 自身 `MinHeight="24"`。

- `TypeSelectorView.InputMinWidth` 默认 120；泛型槽显式 `MinWidth="0" InputMinWidth="0"`。主路径列 1 个 `star`，泛型段按参数个数 n 个 `star`，路径与每个参数大致各占 1/(n+1)。非泛型时泛型列宽 0。
- 表格单元格自然宽 180、最小 140；locale 列 200；操作列 100。

级联（`CascaderView` + `InputWrapper`）：

- 点击字段展开/收起弹层；字段显示 `CascadeSummary`，多级用 `" / "` 连接。
- 候选行分两个点击区：点标签调用 `SelectLevelEntry`，可提交时写值并关闭；点右侧箭头调用 `ExpandLevelEntry`，只加载并展开下一级。
- 非叶子默认不可提交：`LeafOnly` 为真或类型不允许选父节点时，点标签只展开；禁用项点击无效。是否可提交由 `IsNonLeafNodeSelectable` 决定。
- 关闭未提交时 `ReloadCascadeSelection()` 恢复已提交路径，不保留临时父级选择。
- 清除按钮 `CanClear = IsEditable && !Require && HasValue`；`Require` 字段无清除入口。
- 加载态只在级联中、加载中、且尚无候选时显示（`ShowCascadeLoading`），文案 key `avalonia.page.loading`。
- 弹层以输入框左下角为锚点：`Popup Placement="BottomEdgeAlignedLeft"`，目标为整行字段。切换层级时只向右增宽，左边缘不动。

校验与错误：

- 行内错误只挂在渲染值的叶控件：`ShowInlineError = RendersValue && Role == Field`；Struct / Array / Table 容器不显示，表格单元格用 `ShowCellError` 标红。
- `Value` 写入失败时把原文留在 `_pendingText`，`ReadEditorValue` 优先显示草稿；错误优先取 `DataNode.Error`，其次 `VALUE_NOT_VALID`，最后 `avalonia.view.error.invalid`。
- 下一次成功写回会清空草稿与错误；外部值变化时丢弃草稿并显示节点当前值。
- 错误提示同时提供行内 `TextBlock` 与 `ToolTip.Tip=ErrorTip`（仅在非行内时）。

### 3.8 表格

列来源与 `ViewColumns`：

- 列由数组元素 `StructType` 的字段推导，跳过 `InVisible == true`；`Struct` 字段形成两级表头，`StructArray` 字段形成带子操作列的子表。
- `SchemaTableOptions.ViewColumns` 是只读列白名单，支持 `discount.rate` 嵌套路径；非空时只保留命中列并进入 `RowEditorMode`（单元格只读，改动走操作列编辑入口）。
- `ReadOnly` 关闭单元格编辑与增删，但 `ViewColumns` 打开的行编辑入口保留。

宽度：

- `TableView` 从最近祖先 `ScrollViewer` 视口取得预算，并像 WPF 一样把自身 `Width` 钉到预算；`TableWrapper.AvailableWidth` 驱动 `TableColumnLayout.Distribute`：默认铺满、按自然宽等比放大、不足时压到最小宽后仅表体横向滚动。
- 表头与表体共用 `EffectiveColumnWidths` 快照，不各自重算。

行编辑：

- 编辑入口在操作列；弹窗初始尺寸由 `TableRowEditorWindowLayout` 按 Owner 的 2/3 计算（最小 360×240，无 Owner 时 720×560），居中显示。
- 新增行先插入真实元素，再打开编辑弹窗；取消时删除这条临时行，确认失败会留在弹窗并显示 `avalonia.table.error.invalid`。
- 编辑已有行直接改数组中的原始元素，保存前留字段快照，取消时写回快照。
- 非行编辑模式新增只插入空行，直接在单元格编辑。

分页表（`SchemaNode.Avalonia.AppView` 的 `PageTableWrapper`）：

- 页大小由宿主适配器提供；切页先捕获当前页修改，加载目标页后重新套用。
- 跨页修改按主键跟踪，仅主键可确定时启用；删除行按主键记录并在回到该页时保持隐藏。
- 分页表不支持新增，新增入口给出提示；`AcceptChanges` 清空跟踪。
- 总行数变少导致当前页越界时夹到最后一页。
- 画面停用释放当前页并取消在途加载，重新激活时重载；筛选变化回到第一页。

### 3.9 本地化

- `system.language` 专用 `LanguageView`：闭式下拉列已配置 locale，无空选项，选中即写回。
- `system.localestring` 的字段行显示当前值，按钮打开翻译弹窗；弹窗逐语言编辑，支持新增/删除/确认/取消。
- 弹窗打开时没有默认行，新行不预选语言；已有行按语言排序。
- 确认前先把焦点移出文本框以提交正在编辑的文本；写回失败保持弹窗打开并显示 `avalonia.locale.translations.failed`。
- key 归属：`SchemaNode.Avalonia.View/locale` 有 30 条视图 key，`SchemaNode.Avalonia.Man/locale` 有 349 条画面 key，`SchemaNode.Avalonia.AppView/locale` 有 14 条分页表 key；前缀统一为 `avalonia.`，与 WPF 的 `wpf.` 互不共享。

### 3.10 调试面板

- `SchemaView.Debug` 是可继承附加属性（`SchemaView.SetDebug(control, true)`）；打开后 `SchemaView` 在内容右侧追加 🐞 触发器并按需创建弹层，关闭时两者都移出视觉树。
- 弹层内容来自 `SchemaDebug.Describe(node)`，固定三个分区：Properties（名称/值/来源）、Relations（属性/模式/数据/来源）、Errors（访问路径/约束/消息）。
- 面板 320–720 宽、最大高度 420；内容过长在面板内部滚动。
- 行列编辑弹窗通过 `TableRowEditorWindowLayout` 继承宿主 `Debug` 值，弹窗里的编辑器也带调试入口。

## 4. 生命周期

- Wrapper 的归属规则同 WPF：宿主给 wrapper 时只观察不释放；给裸 `DataNode` 时由 `SchemaView` 创建并在离开视觉树/重绑时释放。
- 画面 ViewModel 实现 `INavigationAware` + `IDestructible`：`OnNavigatedFrom` 与 `Destroy` 都调用 `Teardown()`，重复回调安全。
- `Teardown()` 释放画面自己创建的编辑器 wrapper（如命名空间控件、类型控件），再 `_session.Dispose()`；`SchemaScreenSession.Dispose` 依次释放 wrapper → node → context → scope，并把自己从 factory 的 live 集合移除。
- `RegionManager` 在导航时按 WPF 顺序收尾：`OnNavigatedFrom` → `Destroy` → 清空旧 View 的 `DataContext`，让嵌套 `SchemaView` 脱离并释放自建 wrapper。

## 5. 宿主接法（无 Prism）

### 5.1 DI

`ManApp.BuildServices()` 用 `Microsoft.Extensions.DependencyInjection` 构建容器（见 2.1）。没有 Prism 容器、没有 `ViewModelLocator` 命名约定；ViewModel 都显式注册为 Transient。

### 5.2 导航

`RegionManager` 只维护一个当前内容区；各处导航都传常量 `MainContentRegion`，但 `regionName` 目前不参与路由：

- `RequestNavigate(regionName, viewName, callback, parameters)` 从 DI 解析 ViewModel，用 `ManViewFactory` 创建 View，设置 `DataContext`，调用 `OnNavigatedTo`；
- 再对上一个 ViewModel 调用 `OnNavigatedFrom` / `Destroy`，清空旧 View 的 `DataContext`；
- 通过 `CurrentViewChanged` 通知 Shell 替换内容；
- 路由缺失时 `ManViewFactory` 直接抛“No view is registered”，异常写入 `Debug` 输出，并通过导航回调的 `NavigationResult` 返回。

`ManViewFactory` 使用显式映射表（`nameof(TypeListView) → (TypeListViewModel, new TypeListView())`），不走命名约定。

### 5.3 画面

12 个导航画面：`TypeListView`、`TypeDocumentView`、`TypeDemoView`、`AppListView`、`AppDefinitionView`、`AppDemoView`、`AppFieldListView`、`AppFieldDefinitionView`、`AppFieldDemoView`、`AppWorkflowListView`、`FunctionDefinitionView`、`WorkflowConfigView`。Shell 只直接暴露 `TypeListView` 与 `AppListView` 两个入口，其余由画面内部通过 `IRegionManager` 导航。

### 5.4 语言切换

`MainWindowViewModel.SwitchLanguage()` 调 `LanguageService.SwitchAsync(locale)`：

1. `SchemaContextFactory.ApplyLocale` 先把新 locale 推给所有 live context；
2. `LocaleManager.Instance.SetLocale` 刷新 UI 绑定；
3. `LanguageSettingsStore` 持久化，失败不影响已经生效的切换。

### 5.5 视图注册

`ManSchemaViews.CreateRegistry()` 注册 struct fields、relations、enum values、app fields 等表，并通过 `SchemaViewTemplates.Set` / `RegisterType` 覆盖函数调用参数、函数定义等模板。新画面可以通过 `ManViewFactory.Register(...)` 增加路由，不必改核心映射表。

## 6. 与 WPF 版的差异

两端共享 Core 与 Wrapper 语义，下列差异只来自控件能力与宿主技术栈：

| 差异点 | WPF | Avalonia |
| --- | --- | --- |
| 可编辑单层下拉 | 原生 `ComboBox.IsEditable`，候选与自由输入共用同一控件 | `ComboBox` 不可编辑，拆成 `IsSuggest`：`TextBox` + 箭头按钮 + `Popup` |
| Flags 单选形态 | `FlagsView` 只有复选弹层 | `FlagsEnumView` 在 `!IsMulti` 时额外提供单选 `ComboBox` |
| 区间日期 | `RangeDateView` 把 `system.range*` 当作两个子节点的结构体，左右并排两个 `SchemaView` | `RangeDateView` 按 `IsYear` / `IsMonth` / `IsDate` 分别给出 `NumericUpDown`、yyyy-MM 文本框或 `DatePicker`，并分别显示 start/end 错误 |
| 错误提示通道 | 行内 `TextBlock` + 控件 `ToolTip` 绑定 `Error` | 行内 `TextBlock` + `ToolTip.Tip` 绑定 `ErrorTip`（仅在非行内时） |
| 未提交草稿 | 外部值刷新时保留 `_pendingText` | 外部值变化时丢弃草稿 |
| 弹层锚点 | `Placement="Bottom"` 本身就是左下角对齐 | 统一使用 `Placement="BottomEdgeAlignedLeft"` |
| 行编辑弹窗 | 同样按 Owner 的 2/3 计算初值（最小 360×240，无 Owner 720×560），按工作区夹取；始终模态 `ShowDialog` | 尺寸规则相同；无 Owner 时改为非模态打开并按取消处理；弹窗内容禁用横向滚动 |
| 调试面板实现 | `SchemaDebugPanel.xaml` + `SchemaDebug.Describe` | `SchemaDebugPanel.cs` 代码构建同结构面板 |
| locale key | `wpf.` 前缀，View 40 + Man 343；AppView 无独立语言文件 | `avalonia.` 前缀，View 30 + Man 349 + AppView 14 |
| 宿主技术 | Prism Region + `RegisterForNavigation` | 无 Prism：本地 `IRegionManager` + 显式 View/ViewModel 映射 + `INavigationAware` |
