using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using SchemaNode.Avalonia.Man.Infrastructure;
using SchemaNode.Avalonia.Man.Navigation;
using SchemaNode.Context;
using SchemaNode.Property.Common;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Schema.Provider;
using SchemaNode.Utility;
using SchemaNode.Avalonia.Man.Services;
using SchemaNode.Avalonia.Man.Views;
using SchemaNode.Avalonia.View;
using SchemaNode.Avalonia.View.Localization;
using SchemaNode.Avalonia.View.NodeWrapper;

namespace SchemaNode.Avalonia.Man.ViewModels;

/// <summary>
/// "Type definitions" list screen.
///
/// Layout mirrors <c>schema-node-man/src/components/schema.vue</c>:
///   - the namespace control is a SchemaView over a <c>system.schema.namespace.type</c> node, so
///     child namespaces are discovered through the Core entry source (cascading selection) instead
///     of a hand-rolled text box;
///   - the table is read-only and shows name / display / kind plus per-row actions;
///   - view / edit navigate to <see cref="TypeDocumentViewModel"/> through the region; next level drills into
///     a child namespace; delete stays on the row and is delegated to the Core delete API, which enforces
///     the IsUsed / namespace-removal rules.
///
/// Threading: the namespace node and the row list are produced on the thread pool. Only collection
/// and property assignments happen on the dispatcher, so list interaction never blocks the UI thread.
/// </summary>
public class TypeListViewModel : BindableBase, INavigationAware, IDestructible
{
    private const string NamespaceTypeName = "system.schema.namespace.type";
    private const string KindTypeName = "system.schema.node.kind";
    private const string MainContentRegion = "MainContentRegion";

    /// <summary>The root namespace is the empty string; it is a legal selection, not "no selection".</summary>
    private const string RootNamespace = "";

    private readonly SchemaContextFactory _factory;
    private readonly IRegionManager _regionManager;
    private readonly SchemaViewRegistry _registry;
    private readonly DialogService _dialogs;

    private SchemaScreenSession? _session;
    private IValueAccess? _namespaceNode;
    private DataNodeWrapper? _namespaceWrapper;
    private IValueAccess? _kindNode;
    private DataNodeWrapper? _kindWrapper;
    private string _namespace = RootNamespace;
    private string _kindFilter = string.Empty;
    private TypeSchemaRowViewModel[] _allRows = [];
    private string? _status;
    private bool _isBusy;
    private bool _isNavigating;
    private int _loadVersion;

    public TypeListViewModel(SchemaContextFactory factory, IRegionManager regionManager, SchemaViewRegistry registry, DialogService dialogs)
    {
        _factory = factory;
        _regionManager = regionManager;
        _registry = registry;
        _dialogs = dialogs;

        Schemas = new ObservableCollection<TypeSchemaRowViewModel>();

        NewCommand = new DelegateCommand(ExecuteNew);
        RefreshCommand = new DelegateCommand(ExecuteRefresh);
        UpCommand = new DelegateCommand(ExecuteUp, () => CanGoUp);
        ViewCommand = new DelegateCommand<TypeSchemaRowViewModel>(ExecuteView);
        EditCommand = new DelegateCommand<TypeSchemaRowViewModel>(ExecuteEdit);
        CopyCommand = new DelegateCommand<TypeSchemaRowViewModel>(ExecuteCopy);
        EnterCommand = new DelegateCommand<TypeSchemaRowViewModel>(ExecuteEnter);
        DeleteCommand = new DelegateCommand<TypeSchemaRowViewModel>(ExecuteDelete);
        DemoCommand = new DelegateCommand<TypeSchemaRowViewModel>(ExecuteDemo);
    }

    #region Bindable state

    /// <summary>
    /// Namespace node rendered by SchemaView. It is a <c>system.schema.namespace.type</c> value node,
    /// whose entry source produces the cascading child-namespace selector.
    /// </summary>
    public DataNodeWrapper? NamespaceWrapper
    {
        get => _namespaceWrapper;
        private set => SetProperty(ref _namespaceWrapper, value);
    }

    /// <summary>
    /// Node kind filter rendered by SchemaView, a <c>system.schema.node.kind</c> value node.
    /// An empty value means "all kinds", matching schema.vue's <c>!state.type</c>.
    /// </summary>
    public DataNodeWrapper? KindWrapper
    {
        get => _kindWrapper;
        private set => SetProperty(ref _kindWrapper, value);
    }

    /// <summary>Current namespace (full name, e.g. <c>system.schema</c>; empty for the root).</summary>
    public string Namespace
    {
        get => _namespace;
        private set
        {
            if (!SetProperty(ref _namespace, value)) return;
            RaisePropertyChanged(nameof(CanGoUp));
            (UpCommand as DelegateCommand)?.RaiseCanExecuteChanged();
        }
    }

    /// <summary>Whether a parent namespace exists. Every non-root namespace, including the top level, can go up.</summary>
    public bool CanGoUp => _namespace.Length > 0;

    /// <summary>Whether no kind filter is applied, so the "all kinds" placeholder can be shown.</summary>
    public bool IsKindFilterEmpty => string.IsNullOrWhiteSpace(_kindFilter);

    /// <summary>Types and child namespaces of the current namespace.</summary>
    public ObservableCollection<TypeSchemaRowViewModel> Schemas { get; }

    /// <summary>Whether the list is empty (drives the empty-state hint).</summary>
    public bool IsEmpty => Schemas.Count == 0;

    /// <summary>Whether the screen is loading data.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    /// <summary>Operation result / error message.</summary>
    public string? Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public ICommand NewCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand UpCommand { get; }
    public ICommand ViewCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand EnterCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand DemoCommand { get; }

    #endregion

    #region Navigation

    public bool IsNavigationTarget(NavigationContext navigationContext) => false;

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        Teardown();

        try
        {
            _session = _factory.CreateSession();
            // An absent parameter means the root namespace (empty string), mirroring schema.vue's
            // `state.namespace = ""`: the list then shows `system` and its top-level siblings.
            string start = navigationContext.Parameters.GetValue<string>(TypeDocumentViewModel.NamespaceParameter)
                           ?? RootNamespace;
            Namespace = start;
            _ = InitializeAsync(start);
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    public void OnNavigatedFrom(NavigationContext navigationContext) => Teardown();

    public void Destroy() => Teardown();

    #endregion

    private async Task InitializeAsync(string ns)
    {
        IsBusy = true;
        Status = LocaleManager.Instance.Format("avalonia.type.status.loading", DescribeNamespace(ns));
        try
        {
            await RebuildNamespaceControlAsync(ns);
            await RebuildKindControlAsync();
            await LoadListAsync(ns);
        }
        catch (Exception ex)
        {
            Status = LocaleManager.Instance.Format("avalonia.type.status.loadfailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Builds the SchemaView-driven namespace selector on the thread pool: loads the
    /// <c>system.schema.namespace.type</c> schema type, creates a value node seeded with the current
    /// namespace, and wraps it. Only adopting the finished wrapper touches the dispatcher.
    /// </summary>
    private async Task RebuildNamespaceControlAsync(string ns)
    {
        DisposeNamespaceControl();
        ValueControl built = await BuildValueControlAsync(NamespaceTypeName, ns);
        if (built.Node is null)
        {
            Status = built.Error?.Message
                     ?? LocaleManager.Instance.Format("avalonia.type.status.loadfailed", NamespaceTypeName);
            return;
        }

        _namespaceNode = built.Node;
        built.Wrapper!.PropertyChanged += OnNamespaceWrapperChanged;
        NamespaceWrapper = built.Wrapper;
    }

    /// <summary>
    /// Builds the kind selector the same way as the namespace selector. An empty seed means
    /// "all kinds"; the node value drives the in-memory filter only, so the list is not refetched.
    /// </summary>
    private async Task RebuildKindControlAsync()
    {
        DisposeKindControl();
        ValueControl built = await BuildValueControlAsync(KindTypeName, null);
        if (built.Node is null)
        {
            Status = built.Error?.Message
                     ?? LocaleManager.Instance.Format("avalonia.type.status.loadfailed", KindTypeName);
            return;
        }

        _kindNode = built.Node;
        built.Wrapper!.PropertyChanged += OnKindWrapperChanged;
        KindWrapper = built.Wrapper;
    }

    /// <summary>
    /// Creates a value node for <paramref name="typeName"/> seeded with <paramref name="seed"/> and
    /// wraps it on the thread pool. Only adopting the finished wrapper touches the dispatcher.
    /// </summary>
    private async Task<ValueControl> BuildValueControlAsync(string typeName, object? seed)
    {
        SchemaScreenSession? session = _session;
        if (session is null) return new ValueControl(null, null, null);

        SchemaViewRegistry registry = _registry;
        return await Task.Run(async () =>
        {
            IValueAccess? node = null;
            try
            {
                IValueTypeAccess? type = await session.Context
                    .GetNodeTypeAsync<IValueTypeAccess>(typeName).ConfigureAwait(false);
                if (type is null) return new ValueControl(null, null, null);

                node = type.Create(session.Context, seed);
                if (node is null) return new ValueControl(null, null, null);

                // The VM observes this wrapper's Value to drive the cascade/filter; this is the
                // only screen that needs an up-front root wrapper. Other screens pass nodes to views.
                DataNodeWrapper wrapper = NodeWrapperFactory.Create(node, registry);
                return new ValueControl(node, wrapper, null);
            }
            catch (Exception ex)
            {
                if (node is IDisposable disposable) disposable.Dispose();
                return new ValueControl(null, null, ex);
            }
        });
    }

    /// <summary>
    /// Cascading selection writes the value back into the namespace node; mirror it into the list.
    /// The control is already showing the right path, so it is not rebuilt here.
    /// </summary>
    private void OnNamespaceWrapperChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(InputWrapper.Value)) return;

        string value = _namespaceNode?.GetValue<string>() ?? RootNamespace;
        if (value.Equals(_namespace, StringComparison.Ordinal)) return;

        // The cascade also lists concrete types; this screen is a namespace browser, so selecting
        // one of those would leave the list showing a single unrelated row. Restore the node so the
        // selector stays aligned with the list; concrete types are opened through the row actions.
        // Search the unfiltered rows: a kind filter may hide the selected type.
        if (_allRows.FirstOrDefault(row => row.FullName.Equals(value, StringComparison.OrdinalIgnoreCase))
            is { IsNamespace: false })
        {
            _namespaceNode?.TrySetValue(_namespace);
            return;
        }

        Namespace = value;
        _ = ReloadForNamespaceAsync(value);
    }

    /// <summary>
    /// The kind selector's value filters the already-fetched rows, matching schema.vue's
    /// <c>!state.type || p.kind === state.type</c>. Empty/whitespace means all kinds.
    /// </summary>
    private void OnKindWrapperChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(InputWrapper.Value)) return;

        string value = _kindNode?.GetValue<string>() ?? string.Empty;
        if (value.Equals(_kindFilter, StringComparison.OrdinalIgnoreCase)) return;

        _kindFilter = value;
        RaisePropertyChanged(nameof(IsKindFilterEmpty));
        ApplyKindFilter();
    }

    private async Task ReloadForNamespaceAsync(string ns)
    {
        IsBusy = true;
        Status = LocaleManager.Instance.Format("avalonia.type.status.loading", DescribeNamespace(ns));
        try
        {
            await LoadListAsync(ns);
        }
        catch (Exception ex)
        {
            Status = LocaleManager.Instance.Format("avalonia.type.status.loadfailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadListAsync(string ns)
    {
        SchemaScreenSession? session = _session;
        if (session is null) return;

        int version = ++_loadVersion;
        TypeSchemaRowViewModel[] rows = await Task.Run(async () =>
        {
            // A named namespace comes back with its definition and direct children nested, so the
            // rows are taken from the requested namespace entry. The root namespace ("") has no
            // definition of its own: the API returns its direct children (the top-level
            // namespaces) as the result, which must be used as-is instead of searching for "".
            NodeSchema[] schemas = await session.Api.GetSchemaAsync([ns]).ConfigureAwait(false);
            NodeSchema[] children = string.IsNullOrEmpty(ns)
                ? schemas
                : SchemaTreeSearch.Find(schemas, ns)?.Schemas ?? [];
            return children
                .Where(schema => schema.Name is not null && !schema.Name.Contains('<', StringComparison.Ordinal))
                .Select(schema => new TypeSchemaRowViewModel(
                    schema,
                    schema.GetProperty<Display>()?.Value?.ToString(session.Context) ?? schema.Name))
                .OrderBy(row => row.Kind, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        });

        // A newer navigation/selection won: do not apply stale rows.
        if (version != _loadVersion) return;

        // Keep the unfiltered set so a kind change filters in memory instead of refetching.
        _allRows = rows;
        ApplyKindFilter();
    }

    /// <summary>Projects the cached rows through the current kind filter.</summary>
    private void ApplyKindFilter()
    {
        Schemas.Clear();
        foreach (TypeSchemaRowViewModel row in _allRows)
        {
            if (_kindFilter.Length == 0 || row.Kind.Equals(_kindFilter, StringComparison.OrdinalIgnoreCase))
                Schemas.Add(row);
        }
        RaisePropertyChanged(nameof(IsEmpty));

        Status = Schemas.Count == 0
            ? LocaleManager.Instance.Get("avalonia.type.status.empty")
            : LocaleManager.Instance.Format("avalonia.type.status.loaded", Schemas.Count);
    }

    private static string DescribeNamespace(string ns)
        => ns.Length == 0 ? LocaleManager.Instance.Get("avalonia.type.namespace.root") : ns;

    #region Commands

    private async void ExecuteNew()
    {
        try
        {
            await NavigateToDocumentAsync(null, TypeDocumentViewModel.ModeNew);
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    private async void ExecuteRefresh()
    {
        try
        {
            await ReloadForNamespaceAsync(_namespace);
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    private async void ExecuteUp()
    {
        try
        {
            await SetNamespaceAsync(ParentOf(_namespace));
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    private void ExecuteEnter(TypeSchemaRowViewModel? row)
    {
        if (row is null) return;
        try
        {
            if (_namespaceNode?.TrySetValue(row.FullName) == true) return;

            // Fallback only: the selector could not accept the value, so rebuild it with the
            // target namespace instead of leaving the list and the control out of sync.
            _ = SetNamespaceAsync(row.FullName);
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    private async void ExecuteView(TypeSchemaRowViewModel? row)
    {
        if (row is null) return;
        try
        {
            await NavigateToDocumentAsync(row.FullName, TypeDocumentViewModel.ModeView);
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    private async void ExecuteEdit(TypeSchemaRowViewModel? row)
    {
        if (row is null) return;
        try
        {
            // Defense in depth: the edit action is hidden for host-owned definitions. If it is
            // invoked anyway, land on the read-only view instead of an edit screen.
            string mode = row.IsEditable
                ? TypeDocumentViewModel.ModeEdit
                : TypeDocumentViewModel.ModeView;
            await NavigateToDocumentAsync(row.FullName, mode);
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    /// <summary>
    /// Opens the new-definition screen seeded with a copy of this row's definition. The screen
    /// drops the name and source identity; the target is a new type in the row's namespace.
    /// </summary>
    private async void ExecuteCopy(TypeSchemaRowViewModel? row)
    {
        if (row is null) return;
        try
        {
            await NavigateToDocumentAsync(row.FullName, TypeDocumentViewModel.ModeCopy);
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    private async void ExecuteDemo(TypeSchemaRowViewModel? row)
    {
        if (row is null) return;
        try
        {
            await NavigateToDemoAsync(row.FullName);
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    private async void ExecuteDelete(TypeSchemaRowViewModel? row)
    {
        SchemaScreenSession? session = _session;
        if (row is null || session is null) return;

        if (row.IsSystemDefined)
        {
            Status = LocaleManager.Instance.Format("avalonia.type.status.protected", row.FullName);
            return;
        }

        if (row.IsUsed)
        {
            Status = LocaleManager.Instance.Format("avalonia.type.status.inuse", row.FullName);
            return;
        }

        if (!await ConfirmDeleteAsync(row.FullName)) return;

        IsBusy = true;
        try
        {
            // The Core API performs the actual constraint checks (IsUsed, non-empty namespace) and
            // removes both the schema and its storage entry.
            bool removed = await Task.Run(() => session.Api.DeleteSchemaAsync(row.FullName));
            if (!removed)
            {
                Status = LocaleManager.Instance.Format("avalonia.type.status.deletefailed", row.FullName);
                return;
            }

            await LoadListAsync(_namespace);
            Status = LocaleManager.Instance.Format("avalonia.type.status.deleted", row.FullName);
        }
        catch (Exception ex)
        {
            Status = LocaleManager.Instance.Format("avalonia.type.status.deletefailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion

    private async Task SetNamespaceAsync(string ns)
    {
        Namespace = ns;
        IsBusy = true;
        Status = LocaleManager.Instance.Format("avalonia.type.status.loading", DescribeNamespace(ns));
        try
        {
            await RebuildNamespaceControlAsync(ns);
            await LoadListAsync(ns);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task NavigateToDocumentAsync(string? fullName, string mode)
    {
        if (_isNavigating) return;
        _isNavigating = true;
        try
        {
            NavigationParameters parameters = new()
            {
                { TypeDocumentViewModel.ModeParameter, mode },
                { TypeDocumentViewModel.NamespaceParameter, _namespace },
            };
            if (!string.IsNullOrWhiteSpace(fullName))
                parameters.Add(TypeDocumentViewModel.NameParameter, fullName);

            TaskCompletionSource<INavigationResult> completion = new();
            _regionManager.RequestNavigate(
                MainContentRegion,
                nameof(TypeDocumentView),
                result => completion.TrySetResult(result),
                parameters);

            INavigationResult outcome = await completion.Task;
            if (outcome.Success) return;

            Status = LocaleManager.Instance.Format(
                "avalonia.type.status.navfailed",
                outcome.Exception?.Message ?? nameof(TypeDocumentView));
        }
        finally
        {
            _isNavigating = false;
        }
    }

    /// <summary>
    /// Opens the demo screen for the given type. The type name travels as a navigation parameter
    /// (the demo screen used to be a fixed navigation entry with a hard-coded system.string).
    /// </summary>
    private async Task NavigateToDemoAsync(string fullName)
    {
        if (_isNavigating) return;
        _isNavigating = true;
        try
        {
            TaskCompletionSource<INavigationResult> completion = new();
            _regionManager.RequestNavigate(
                MainContentRegion,
                nameof(TypeDemoView),
                result => completion.TrySetResult(result),
                new NavigationParameters
                {
                    { TypeDemoViewModel.TypeParameter, fullName },
                    // Return to the namespace the demo was opened from, not to the default level.
                    { TypeDemoViewModel.NamespaceParameter, _namespace },
                });

            INavigationResult outcome = await completion.Task;
            if (outcome.Success) return;

            Status = LocaleManager.Instance.Format(
                "avalonia.type.status.navfailed",
                outcome.Exception?.Message ?? nameof(TypeDemoView));
        }
        finally
        {
            _isNavigating = false;
        }
    }

    private static string ParentOf(string ns)
    {
        int index = ns.LastIndexOf('.');
        // A single segment (e.g. "system") is a top-level namespace; its parent is the root, "".
        return index > 0 ? ns[..index] : RootNamespace;
    }

    private Task<bool> ConfirmDeleteAsync(string fullName)
        => _dialogs.ConfirmAsync(
            LocaleManager.Instance.Format("avalonia.type.status.confirmdelete", fullName),
            LocaleManager.Instance.Get("avalonia.type.confirm.caption"));

    private void Teardown()
    {
        DisposeNamespaceControl();
        DisposeKindControl();
        _session?.Dispose();
        _session = null;
        _loadVersion++;
        _allRows = [];
        Schemas.Clear();
        RaisePropertyChanged(nameof(IsEmpty));
    }

    /// <summary>Releases in reverse order: wrapper first (subscriptions), then the value node.</summary>
    private void DisposeNamespaceControl()
    {
        if (_namespaceWrapper is not null)
        {
            _namespaceWrapper.PropertyChanged -= OnNamespaceWrapperChanged;
            _namespaceWrapper.Dispose();
        }
        _namespaceWrapper = null;
        NamespaceWrapper = null;

        if (_namespaceNode is IDisposable disposable)
            _session?.ReleaseNode(disposable);
        _namespaceNode = null;
    }

    /// <summary>Same teardown order for the kind selector.</summary>
    private void DisposeKindControl()
    {
        if (_kindWrapper is not null)
        {
            _kindWrapper.PropertyChanged -= OnKindWrapperChanged;
            _kindWrapper.Dispose();
        }
        _kindWrapper = null;
        KindWrapper = null;

        if (_kindNode is IDisposable disposable)
            _session?.ReleaseNode(disposable);
        _kindNode = null;
    }

    private sealed record ValueControl(IValueAccess? Node, DataNodeWrapper? Wrapper, Exception? Error);
}
