# 06. WPF 接入

本章面向「在自有 WPF 应用里接一套 Schema 视图」的场景。实现仍分两层：`SchemaNode.WPF.View` 只依赖 Core，负责把 `DataNode` 渲染成可编辑控件；`SchemaNode.WPF.Man` 是基于 Prism 的可运行管理端，演示完整接入方式。视图层采用「Wrapper + View 共生」：每个 Wrapper 只负责一个节点的数据形状，与自己的 `UserControl` 和 `DataTemplate` 成对出现，递归由嵌套的 `SchemaView` 完成。

本章按「工程 → 初始化 → 绑定 → 生命周期 → Prism 宿主」推进。每一步都给出可直接复制的最小代码。

| 你要做的事 | 用到的程序集 / 类型 | 对应小节 |
| --- | --- | --- |
| 只渲染表单，不需要 Schema API | `SchemaNode.WPF.View` | 1.1、3 |
| 需要本地或远程读写 schema | `SchemaNode.WPF.View` + `SchemaNode.App` 的 `ISchemaApiProvider` | 1.2、2 |
| 需要可导航的管理端骨架 | `SchemaNode.WPF.Man`（Prism 宿主示例） | 1.3、5 |

---

## 1. 工程与依赖

### 1.1 只渲染：引用 SchemaNode.WPF.View

`SchemaNode.WPF.View` 的输出是普通 WPF 类库，不依赖 Prism，也不会主动访问任何后端。视图层只做两件事：把 `DataNode` 包成 `DataNodeWrapper`，再按节点形状选择成对的 `UserControl` 与 `DataTemplate`。宿主只需要加一个项目引用：

```xml
<ProjectReference Include="..\SchemaNode.WPF.View\SchemaNode.WPF.View.csproj" />
```

目标框架与前置条件：

| 项 | 要求 |
| --- | --- |
| 目标框架 | `net10.0-windows` |
| WPF | `UseWPF=true` |
| 依赖 | 只引用 `SchemaNode.Core`，不引入 App 层与 Prism |

见 [SchemaNode.WPF.View.csproj](../../../SchemaNode.WPF.View/SchemaNode.WPF.View.csproj)。宿主自己决定 `DataNode` 从哪里来、何时创建；如果把裸 `DataNode` 直接交给 `SchemaView`，包装与释放由它负责（见 4.1）。

### 1.2 需要 Schema 数据：引用 SchemaNode.App

渲染之外，读写类型定义与应用定义走 [ISchemaApiProvider.cs](../../../SchemaNode.App/Api/Client/ISchemaApiProvider.cs)。它提供两种实现，生命周期不同：

| 实现 | 生命周期 | 用途 |
| --- | --- | --- |
| `LocalSchemaApiProvider` | 每个画面一个，构造时传入本画面的 `SchemaContext` | 进程内直接读写，不经过 HTTP |
| `RemoteSchemaApiProvider` | 无状态，可全局共享 | 通过 HTTP 访问服务端 |

本地实现直接复用画面 context，因此 `CallFunctionAsync` 里的 `SetAccess(app, target)` 只写在本画面上，不会跨画面污染；context 的 `Locale` 也由 `SchemaContextFactory` 统一同步，语言切换后新生成的错误与校验文案随之更新。远程实现只保存一个 `HttpClient` 与 `SchemaApiProviderOptions.BaseAddress`。

项目引用：

```xml
<ProjectReference Include="..\SchemaNode.App\SchemaNode.App.csproj" />
```

### 1.3 Prism 宿主：参考 SchemaNode.WPF.Man

`SchemaNode.WPF.Man` 已经是一个可运行的 Prism 宿主（`WinExe`），包含 Region、导航、七个画面与注册代码：类型定义、类型定义文档、类型演示、应用定义、应用演示、工作流配置、函数定义。接入新应用时，推荐把它当作模板复制相关文件，而不是把可执行项目直接引用进业务工程。

它依赖的包与项目：

```xml
<PackageReference Include="Prism.DryIoc" Version="9.0.537" />

<ProjectReference Include="..\SchemaNode.Core\SchemaNode.Core.csproj" />
<ProjectReference Include="..\SchemaNode.App\SchemaNode.App.csproj" />
<ProjectReference Include="..\SchemaNode.WPF.View\SchemaNode.WPF.View.csproj" />
```

见 [SchemaNode.WPF.Man.csproj](../../../SchemaNode.WPF.Man/SchemaNode.WPF.Man.csproj)。

---

## 2. 初始化：每个画面一个 SchemaContext

### 2.1 runtime 共享，context 隔离

`ISchemaRuntime` 是进程内单例，类型缓存、根命名空间、App 运行时都在它上面；`SchemaContext` 是 scoped 的，承载全量模式、访问上下文、上下文项、语言以及节点订阅。因此初始化只需要做一次 runtime，画面只新增自己的 scope 与 context。

| 对象 | 生命周期 | 说明 |
| --- | --- | --- |
| `ISchemaRuntime` | 进程单例 | 类型缓存、根命名空间；启动时初始化一次 |
| `SchemaContext` | 每画面 scoped | `FullMode`、`Access`、`ContextItems`、`Locale`、锁加载归属 |
| `DataNode` / `DataNodeWrapper` | 每画面 | 数据节点、订阅、条目缓存与释放归属 |

见 [SchemaContextFactory.cs](../../../SchemaNode.WPF.Man/Services/SchemaContextFactory.cs)。

### 2.2 创建顺序不可反

每个画面在激活时创建一个会话。顺序是硬约束：**先拿到 context，立刻置 `FullMode = true`，再返回给画面去加载类型、创建节点**。

```csharp
public SchemaScreenSession CreateSession()
{
    IServiceScope scope = _runtimeServices.CreateScope();
    SchemaContext context = scope.ServiceProvider.GetRequiredService<SchemaContext>();

    // 创建后立刻开启全量模式，必须早于任何类型加载与节点创建
    context.FullMode = true;
    context.Locale = _locale;

    ISchemaApiProvider api = _sharedRemote ?? new LocalSchemaApiProvider(context);
    return new SchemaScreenSession(scope, context, api, Unregister);
}
```

画面拿到会话后，才允许执行 `GetNodeTypeAsync`、`Create` 与把节点交给 `SchemaView`：

```csharp
public void OnNavigatedTo(NavigationContext navigationContext)
{
    Teardown();
    _session = _factory.CreateSession();
    Editor.Attach(_session);
    _ = InitializeAsync();   // 内部 Task.Run，绝不同步等待
}
```

### 2.3 为什么 FullMode 必须在建节点之前

`DataNode.FullMode` 直接读取 `Context.FullMode`。非全量模式下的节点是「哑节点」：

- `Subscribe` 返回静态空订阅，不会推送值变化；
- `SubscribeViolated` 不触发校验状态回调；
- `StructType.Create` 不会附带 `DataNodeType` 元属性，结构体字段不会按类型建节点；
- `ScalarNode` 构造直接返回，`EntrySource` / `WhiteList` / `Valid` 等属性订阅全部跳过，级联与热替换都不生效。

所以晚设置 `FullMode` 不会「补上」之前创建的节点，只会让新节点正常、旧节点永远沉默。必须在建节点之前完成。

### 2.4 画面级 API provider

本地 provider 在会话创建时构造，参数就是本画面的 context：

```csharp
ISchemaApiProvider api = _sharedRemote ?? new LocalSchemaApiProvider(context);
```

远程 provider 无状态，可以在宿主启动时创建一次并共享。两种模式对画面代码是同一个接口，画面只依赖 `session.Api`；会话销毁时由 `SchemaScreenSession` 统一反序释放数据节点、context 与 scope。

---

## 3. 绑定：Wrapper + View 共生

### 3.1 包一个节点

`SchemaView` 接受 `DataNode` 或现成的 `DataNodeWrapper`。默认路径只传节点：视图在 `Loaded` 时按注册表解析并创建 wrapper，在 `Unloaded`、重新绑定或节点替换时释放；宿主已经持有 wrapper 时才走 wrapper 路径。

```csharp
DataNode node = (DataNode)((IValueTypeAccess)type).Create(session.Context);
schemaView.Node = node;   // 视图在加载时解析并包装
```

```xml
<view:SchemaView Node="{Binding AppNode}" Registry="{Binding Registry}" />
```

见 [SchemaView.xaml.cs](../../../SchemaNode.WPF.View/SchemaView.xaml.cs)、[NodeWrapperFactory.cs](../../../SchemaNode.WPF.View/NodeWrapper/NodeWrapperFactory.cs)、[SchemaViewRegistry.cs](../../../SchemaNode.WPF.View/SchemaViewRegistry.cs) 与 [SchemaTemplates.xaml](../../../SchemaNode.WPF.View/Templates/SchemaTemplates.xaml)。Wrapper 负责绑定、线程切换、错误与释放；View 只负责布局与交互，两者在节点物化时一一对应。

### 3.2 Wrapper 与视图成对

每个具体 Wrapper 都有自己的 `UserControl` 与 `DataTemplate` 资源键：

| Wrapper | UserControl | 模板键 | 形态 |
| --- | --- | --- | --- |
| `InputWrapper` | `InputView.xaml` | `SchemaInput` | 单值输入、级联下拉、多选弹层 |
| `DateWrapper` | `DateView.xaml` | `SchemaDate` | `DatePicker` |
| `BoolWrapper` | `BoolView.xaml` | `SchemaBoolean` | `CheckBox` |
| `StructWrapper` | `StructView.xaml` | `SchemaStruct` | 标签 + 递归 `SchemaView` |
| `ArrayWrapper` | `ArrayView.xaml` | `SchemaArray` | 可增删的列表 |
| `TableWrapper` | `TableView.xaml` | `SchemaTable` | 结构体数组表格 |
| `FallbackWrapper` | 内联只读 `TextBox` | `SchemaFallback` | 未知节点形状 |

`DataNodeWrapper` 是公共基类，承担 `INotifyPropertyChanged`、`INotifyDataErrorInfo`、线程调度、一次性退订，以及权限状态（`Require`、`ReadOnly`、`Disable`、`DisplayOnly`）与派生的 `IsEditable` / `IsRequired` / `IsOptional`。`InputWrapper` 负责全部入口相关内容：候选、级联层级、多选状态与 `AsSuggest` / `LeafOnly`。Date 与 Bool 直接继承 `DataNodeWrapper`：它们需要权限状态，但从不带入口来源。所有 Wrapper 共享同一个 `Registry`、`Role` 与 `FieldName`。

### 3.3 按节点形状分流

`NodeWrapperFactory` 先查注册表里的自定义 wrapper 工厂；没有声明工厂时，再按固定的形状顺序分流（宿主不要自己按 `Kind` 写分支）：

1. `EnumArrayNode` → `InputWrapper`（多选弹层）；
2. `ArrayNode` → `Registry.ShouldUseTable` 为真时 `TableWrapper`，否则 `ArrayWrapper`；
3. 实现 `IEnumerable<IValueAccess>` 但不是 `ArrayNode` 的列表节点（例如系统函数实参）→ `ArrayWrapper`；
4. `StructNode` → `StructWrapper`；
5. `DateNode` → `DateWrapper`；
6. `BoolNode` → `BoolWrapper`；
7. `ScalarNode` → `InputWrapper`（没有候选时退化为普通文本编辑）；
8. 其它形状 → `FallbackWrapper`。

入口来源由 Core 异步解析并去抖；`EntrySourceVersion` 每次变化，`InputWrapper` 都会发起可取消的加载，因此命名空间、类型这类级联选择器会在快照到达后从普通文本自动升级为下拉。

### 3.4 候选条目：单层、级联与多选

`InputWrapper` 对外暴露 `Entries`、`Levels`、`MultiEntries` 与 `HasEntries`，并给出三个互斥的显示分支：

```csharp
bool multi = wrapper.IsMulti;          // EnumArrayNode：复选框弹层
bool cascading = wrapper.IsCascading;  // 多级入口来源：每级一个下拉
bool plain = wrapper.IsPlainText;      // 没有任何候选：普通 TextBox
```

级联层数由 Core 的 entry source 决定，因此 `IsCascading` 在应用快照时判定，而不是构造时写死。每次 `EntrySourceVersion` 变化会取消上一轮加载并重新调度，结果在 dispatcher 上一次性批量写入集合；`SelectedEntry`、`Selected`、`IsSelected` 的写回都经过同一个 `Value` setter。

### 3.5 模板解析与注册

解析直接以 `IValueAccess` 节点为输入，返回 `SchemaViewRegistration`（模板键 + 可选 wrapper 工厂），不会为了解析先创建 wrapper。顺序固定：

1. 宿主显式覆盖：传入现成 wrapper 时读取它的 `TemplateKeyOverride`；
2. 类型注册：先查自身类型，再沿 `ScalarType.BaseType` 递归；角色不是 `Field` 时回退 `Field`，皮肤不是 `default` 时回退 `default`；
3. kind 注册：内置 bool / int / decimal / string / enum / object / struct / date，array 由 `ResolveArray` 按 `ShouldUseTable` 动态选择表格或列表；
4. 节点形状默认视图：已知形状取对应视图，标量以 `SchemaInput` 兜底，未知形状降级到只读 `SchemaFallback`。

`SchemaViewRegistry` 提供三个注册入口：`RegisterKind` 绑定某种节点形状，`RegisterType` 覆盖某个类型（可带角色、皮肤与 `asSingle`），`RegisterTable` 是「结构体数组按表格渲染」的便捷写法。三者都接受可选的 `wrapperFactory: Func<IValueAccess, DataNodeWrapper>`；不声明时沿用该形状的内置 wrapper（Input / Date / Bool / Struct / Array / Table / Fallback）。重复注册同一类型会替换旧项，这正是宿主覆盖内置视图的方式。

```csharp
SchemaViewRegistry registry = SchemaViewRegistry.CreateDefault();
SchemaViewRegistration reg = registry.Resolve(node, SchemaViewRole.Field, "default");
DataNodeWrapper? custom = reg.WrapperFactory?.Invoke(node);

registry.RegisterKind(Constant.SCHEMA_KIND_NODE_OBJECT, SchemaTemplateKeys.Struct);
registry.RegisterType("system.identifier", "SchemaImmutableTextCell", SchemaViewRole.Cell,
    wrapperFactory: n => new IdentifierWrapper((DataNode)n));
registry.RegisterTable("system.schema.struct.fields");
```

类型注册的查找结果按 `(type, role, skin)` 缓存，注册表变更时整体失效；缓存的是注册项而不是 wrapper 实例，所以同一节点在不同视图里仍会得到独立 wrapper。解析出资源键后，`SchemaTemplateSelector` 先查容器可命中的宿主资源，再查库内置的 `Templates/SchemaTemplates.xaml`；宿主因此可以覆盖任意键，也可以注册新键，而不必修改库代码。`Role=Cell` 是表格单元格的紧凑形态，不渲染行内错误块。

### 3.6 可见性、校验与本地化

`SchemaView` 只有三个依赖属性：`Node`（接受 `DataNode` 或 `DataNodeWrapper`）、`Role`（`SchemaViewRole`）与 `Skin`（字符串）；`IsVisible` 不是第四个属性，而是由 `DataNodeWrapper.IsVisible` 单向前向绑定到内部 `ContentPresenter` 的 `Visibility`，Core 的 `Visible` / `InVisible` / `Attach` 变化会直接反映到界面。`Role` 与 `Skin` 会传给最近的 `SchemaView`，由模板选择器参与解析。

`Value` 的 setter 写入失败时不会吞掉原因：优先透出 `DataNode.Error`，没有时给出精确的转换失败信息；`GetErrors` 返回同一真实错误，缺失时回落为实际被违反的约束名。

```xml
<view:SchemaView Node="{Binding Node}" Role="Field" Skin="default" />
<TextBox Text="{Binding Value, Mode=TwoWay, UpdateSourceTrigger=LostFocus,
                ValidatesOnNotifyDataErrors=True}" />
```

文案统一走 `LocaleManager.Instance` 的 `Version` 绑定：`Content="{Binding Source={x:Static loc:LocaleManager.Instance}, Path=Version, Converter={x:Static loc:LocalizedTextConverter.Instance}, ConverterParameter=wpf.button.new}"`。切换语言时 `Version` 递增，所有 XAML 文案原地刷新；Wrapper 的 `Label` / `Error` 与表格列头也会在同一事件上重算，不需要重建视觉树。`zhCN.json` 与 `enUS.json` 的 key 必须成对出现，缺失时回落为 key 本身。

---

## 4. 生命周期

### 4.1 wrapper 所有权与反序销毁

`SchemaView` 接受两种上下文，所有权规则不同：宿主传入裸 `DataNode` 时，视图在 `Loaded` 物化时按注册表创建 Wrapper 并持有它（`_owned`），在 `Unloaded`、重新绑定、`Role` / `Registry` 变化或节点替换时释放；宿主传入现成的 `DataNodeWrapper` 时，Wrapper 由宿主创建、由宿主 `Dispose`，`SchemaView` 只解除自己的绑定。`_pendingNode` 让未加载的视图只记住节点，不提前包装；`_hasContent` 守卫让 `Loaded` / `DataContextChanged` 的重复回调直接返回，不会重建整棵子树。

画面关闭时严格反序：**先 Dispose 所有 Wrapper（解除订阅），再销毁 DataNode，最后销毁 `SchemaContext` 与 scope**。

```csharp
public void Teardown()
{
    Editor.Dispose();          // Wrapper 树先行退订
    _session?.Dispose();       // 剩余 DataNode → Context → Scope
}
```

会话内部的兜底顺序：

```csharp
for (int i = _nodes.Count - 1; i >= 0; i--)
    _nodes[i].Dispose();
Context.Dispose();
_scope.Dispose();
```

`DataNode.Dispose()` 是同步的，`StructNode` / `ArrayNode` 会递归释放子节点；`SchemaContext.Dispose()` 也是同步的，只清理上下文项。会话销毁前会先从 `SchemaContextFactory` 的活动列表移除，避免语言切换触达已死亡的 context。

### 4.2 数组与表格重绑

`StructWrapper` 与 `TableWrapper` 按节点身份对子项做增量对账：值变化保留同一批 Wrapper，只有 schema 变化（例如 `Attach` 切换字段集）才增删条目；表格列由元素 `StructType` 的字段一次推导，字段带 `InVisible` 的列会被跳过。宿主自己维护数组时，重绑必须先退订再清空：

```csharp
foreach (DataNodeWrapper child in Children)
    child.Dispose();
Children.Clear();
// 再按新元素重建 wrapper
```

这样旧 Wrapper 不会继续接收事件，虚拟化容器回收时也不会访问已释放的订阅。

### 4.3 保存后的失效重建

保存类型会让对应类型 `Loaded=false`，旧的 `DataNode` 变成死引用。不要继续复用，直接在本画面内失效重建：

```csharp
await Editor.ReloadSchemaAsync(_fullName, reload: true);
```

`ReloadSchemaAsync` 先 Dispose 旧 Wrapper 树与旧 DataNode，再用 `GetSchemaAsync` 读回最新定义、按 `reload: true` 重新解析类型并创建新节点。重建只影响本画面，其它画面不受影响；类型定义文档保存成功后会把模式切到 `edit` 并立即重载。

---

## 5. Prism 宿主接法

### 5.1 Region 与导航

Shell 中声明 Region，导航目标注入此处：

```xml
<ContentControl prism:RegionManager.RegionName="MainContentRegion" />
```

启动后导航到默认画面：

```csharp
Container.Resolve<IRegionManager>().RequestNavigate(
    "MainContentRegion", nameof(TypeListView),
    result => { if (!result.Success) Report(result.Exception); });
```

见 [MainWindow.xaml](../../../SchemaNode.WPF.Man/MainWindow.xaml) 与 [App.xaml.cs](../../../SchemaNode.WPF.Man/App.xaml.cs)。`CreateShell` 显式解析并赋给 Shell 的 `DataContext`，不依赖 Prism 对 `.Views` 命名空间的约定；启动导航在 `OnInitialized` 之后异步执行，失败信息写回 Shell 的 `StatusMessage`，不会静默留在空 Region。

### 5.2 RegisterTypes

注册分三步：构建 runtime、注册画面级工厂与视图注册表、注册视图与 ViewModel。

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

ViewModel 注册为瞬态（`Register<T>()` 默认瞬态），每次导航都会得到新实例；不要注册成单例，否则会和画面级 context 冲突。`ManSchemaViews.CreateRegistry()` 在内置默认之上注册结构体字段、关系、枚举值与 App 字段四张表，并为 `system.identifier` 指定只读单元格模板。runtime 初始化（`InitSchemaRuntimeAsync`）同样在 `OnInitialized` 之后异步执行，不在 `RegisterTypes` 里同步等待。

导航按钮用 `DelegateCommand`，并 await `INavigationResult`：

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

### 5.3 画面 ViewModel 与类型定义屏

画面 ViewModel 实现 `INavigationAware` 与 `IDestructible`，在导航激活时才建会话：

```csharp
public void OnNavigatedTo(NavigationContext navigationContext)
{
    Teardown();
    _session = _factory.CreateSession();
    _ = InitializeAsync(navigationContext);
}
```

[TypeListView.xaml](../../../SchemaNode.WPF.Man/Views/TypeListView.xaml) 的命名空间控件就是 `SchemaView`，节点类型为 `system.schema.namespace.type`，由 Core 的 entry source 提供逐级子命名空间；选中具体类型会回写节点，使选择器与列表保持一致。这里是 ViewModel 主动持有根 Wrapper 的唯一例外：VM 需要观察 `InputWrapper.Value` 才能驱动级联并回写选中项。列表是只读的 name / display / kind 表加一列操作，行数据是 `NodeSchema` 而不是 `DataNode`，所以用原生 `ListView + GridView` 承载，而不是 `TableView`；`TableView` 仍用于结构体数组（结构体字段、关系、枚举值、App 字段），由 `RegisterTable` 注册后由 `TableWrapper` 驱动。

[TypeDocumentView.xaml](../../../SchemaNode.WPF.Man/Views/TypeDocumentView.xaml) 用路由参数 `mode` / `name` / `namespace` 承载查看、编辑与新建三种形态：查看时 `IsEditable=false`，编辑与新建可保存；内容由 [SchemaEditorView.xaml](../../../SchemaNode.WPF.Man/Views/SchemaEditorView.xaml) 渲染 `system.schema.node.schema` 定义节点，`HideNameBar` 隐藏列表屏已有的名称栏。保存走 `Task.Run(SaveSchemaAsync)`，成功后重载；返回按钮带 `namespace` 参数回到 `TypeListView`，列表停在原命名空间。

除命名空间控件外，ViewModel 只传节点，不再调用 `NodeWrapperFactory.Create`：

| 画面 | 启动时是否提前创建 Wrapper |
| --- | --- |
| 类型定义 | 是，仅命名空间控件；VM 观察 `InputWrapper.Value` 驱动级联与回写 |
| 类型定义文档 | 否；`SchemaEditorViewModel` 只持有 `Node` |
| 类型演示 | 否；编辑器只持有 `Node` |
| 应用定义 | 否；`AppNode` 与 `FieldsNode` 都是节点 |
| 应用演示 | 否；不使用 SchemaView |
| 工作流配置 | 否；属性面板直接绑定选中节点的 `Node` |
| 函数定义 | 否；参数、返回值与编辑器都传节点 |

`OnNavigatedFrom` 与 `Destroy` 都调用 `Teardown`，保证重复回调也安全。

### 5.4 避免导航缓存旧 context

Prism 的 Region 默认会保留视图。四件事一起做，才能保证 context 与画面生命周期一致：

| 机制 | 作用 |
| --- | --- |
| `[RegionMemberLifetime(KeepAlive = false)]` | 离开 Region 时移除 View，不复用 |
| `IsNavigationTarget => false` | 导航请求不会路由到旧实例 |
| `Register<T>()` 瞬态 ViewModel | 每次导航得到新 ViewModel |
| `OnNavigatedFrom` / `Destroy` → `Teardown` | 旧 context 必然被销毁 |

见画面 View 的代码隐藏文件，例如 [TypeListView.xaml.cs](../../../SchemaNode.WPF.Man/Views/TypeListView.xaml.cs)。
