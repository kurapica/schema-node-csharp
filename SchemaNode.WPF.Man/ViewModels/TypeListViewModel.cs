using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using Prism.Navigation.Regions;
using SchemaNode.Context;
using SchemaNode.Property.Common;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.WPF.Man.Services;
using SchemaNode.WPF.Man.Views;
using SchemaNode.WPF.View;
using SchemaNode.WPF.View.Localization;
using SchemaNode.WPF.View.NodeWrapper;

namespace SchemaNode.WPF.Man.ViewModels;

/// <summary>
/// "类型定义" list screen.
///
/// Layout mirrors <c>schema-node-man/src/components/schema.vue</c>:
///   - the namespace control is a SchemaView over a <c>system.schema.namespace.type</c> node, so
///     child namespaces are discovered through the Core entry source (cascading selection) instead
///     of a hand-rolled text box;
///   - the table is read-only and shows name / display / kind plus per-row actions;
///   - 查看 / 编辑 navigate to <see cref="TypeDocumentViewModel"/> through the region; 下级 drills into
///     a child namespace; 删除 stays on the row and is delegated to the Core delete API, which enforces
///     the IsUsed / namespace-removal rules.
///
/// Threading: the namespace node and the row list are produced on the thread pool. Only collection
/// and property assignments happen on the dispatcher, so list interaction never blocks the UI thread.
/// </summary>
public class TypeListViewModel : BindableBase, INavigationAware, IDestructible
{
    private const string NamespaceTypeName = "system.schema.namespace.type";
    private const string MainContentRegion = "MainContentRegion";
    private const string DefaultNamespace = "system";

    private readonly SchemaContextFactory _factory;
    private readonly IRegionManager _regionManager;
    private readonly SchemaViewRegistry _registry;

    private SchemaScreenSession? _session;
    private IValueAccess? _namespaceNode;
    private DataNodeWrapper? _namespaceWrapper;
    private string _namespace = DefaultNamespace;
    private string? _status;
    private bool _isBusy;
    private bool _isNavigating;
    private int _loadVersion;

    public TypeListViewModel(SchemaContextFactory factory, IRegionManager regionManager, SchemaViewRegistry registry)
    {
        _factory = factory;
        _regionManager = regionManager;
        _registry = registry;

        Schemas = new ObservableCollection<TypeSchemaRowViewModel>();

        NewCommand = new DelegateCommand(ExecuteNew);
        RefreshCommand = new DelegateCommand(ExecuteRefresh);
        UpCommand = new DelegateCommand(ExecuteUp, () => CanGoUp);
        ViewCommand = new DelegateCommand<TypeSchemaRowViewModel>(ExecuteView);
        EditCommand = new DelegateCommand<TypeSchemaRowViewModel>(ExecuteEdit);
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

    /// <summary>Current namespace (full name, e.g. <c>system.schema</c>).</summary>
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

    /// <summary>Whether a parent namespace exists.</summary>
    public bool CanGoUp => _namespace.Contains('.', StringComparison.Ordinal);

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
            string start = navigationContext.Parameters.GetValue<string>(TypeDocumentViewModel.NamespaceParameter)
                           is { Length: > 0 } value
                ? value
                : DefaultNamespace;
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
        Status = LocaleManager.Instance.Format("wpf.type.status.loading", ns);
        try
        {
            await RebuildNamespaceControlAsync(ns);
            await LoadListAsync(ns);
        }
        catch (Exception ex)
        {
            Status = LocaleManager.Instance.Format("wpf.type.status.loadfailed", ex.Message);
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
        SchemaScreenSession? session = _session;
        if (session is null) return;

        SchemaViewRegistry registry = _registry;
        NamespaceControl built = await Task.Run(async () =>
        {
            IValueAccess? node = null;
            try
            {
                IValueTypeAccess? type = await session.Context
                    .GetNodeTypeAsync<IValueTypeAccess>(NamespaceTypeName).ConfigureAwait(false);
                if (type is null) return new NamespaceControl(null, null, null);

                node = type.Create(session.Context, ns);
                if (node is null) return new NamespaceControl(null, null, null);

                // The VM observes this wrapper's Value to drive the namespace cascade; this is the
                // only screen that needs an up-front root wrapper. Other screens pass nodes to views.
                DataNodeWrapper wrapper = NodeWrapperFactory.Create(node, registry);
                return new NamespaceControl(node, wrapper, null);
            }
            catch (Exception ex)
            {
                if (node is IDisposable disposable) disposable.Dispose();
                return new NamespaceControl(null, null, ex);
            }
        });

        if (built.Node is null)
        {
            Status = built.Error?.Message
                     ?? LocaleManager.Instance.Format("wpf.type.status.loadfailed", NamespaceTypeName);
            return;
        }

        _namespaceNode = built.Node;
        built.Wrapper!.PropertyChanged += OnNamespaceWrapperChanged;
        NamespaceWrapper = built.Wrapper;
    }

    /// <summary>
    /// Cascading selection writes the value back into the namespace node; mirror it into the list.
    /// The control is already showing the right path, so it is not rebuilt here.
    /// </summary>
    private void OnNamespaceWrapperChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(InputWrapper.Value)) return;

        string? value = _namespaceNode?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(value) || value.Equals(_namespace, StringComparison.Ordinal)) return;

        // The cascade also lists concrete types; this screen is a namespace browser, so selecting
        // one of those would leave the list showing a single unrelated row. Restore the node so the
        // selector stays aligned with the list; concrete types are opened through the row actions.
        if (Schemas.FirstOrDefault(row => row.FullName.Equals(value, StringComparison.OrdinalIgnoreCase))
            is { IsNamespace: false })
        {
            _namespaceNode?.TrySetValue(_namespace);
            return;
        }

        Namespace = value;
        _ = ReloadForNamespaceAsync(value);
    }

    private async Task ReloadForNamespaceAsync(string ns)
    {
        IsBusy = true;
        Status = LocaleManager.Instance.Format("wpf.type.status.loading", ns);
        try
        {
            await LoadListAsync(ns);
        }
        catch (Exception ex)
        {
            Status = LocaleManager.Instance.Format("wpf.type.status.loadfailed", ex.Message);
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
            NodeSchema[] schemas = await session.Api.GetSchemaAsync([ns]).ConfigureAwait(false);
            return schemas
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

        Schemas.Clear();
        foreach (TypeSchemaRowViewModel row in rows)
            Schemas.Add(row);
        RaisePropertyChanged(nameof(IsEmpty));

        Status = rows.Length == 0
            ? LocaleManager.Instance.Get("wpf.type.status.empty")
            : LocaleManager.Instance.Format("wpf.type.status.loaded", rows.Length);
    }

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

    private async void ExecuteEnter(TypeSchemaRowViewModel? row)
    {
        if (row is null) return;
        try
        {
            await SetNamespaceAsync(row.FullName);
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
            await NavigateToDocumentAsync(row.FullName, TypeDocumentViewModel.ModeEdit);
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
            Status = LocaleManager.Instance.Format("wpf.type.status.protected", row.FullName);
            return;
        }

        if (row.IsUsed)
        {
            Status = LocaleManager.Instance.Format("wpf.type.status.inuse", row.FullName);
            return;
        }

        if (!ConfirmDelete(row.FullName)) return;

        IsBusy = true;
        try
        {
            // The Core API performs the actual constraint checks (IsUsed, non-empty namespace) and
            // removes both the schema and its storage entry.
            bool removed = await Task.Run(() => session.Api.DeleteSchemaAsync(row.FullName));
            if (!removed)
            {
                Status = LocaleManager.Instance.Format("wpf.type.status.deletefailed", row.FullName);
                return;
            }

            await LoadListAsync(_namespace);
            Status = LocaleManager.Instance.Format("wpf.type.status.deleted", row.FullName);
        }
        catch (Exception ex)
        {
            Status = LocaleManager.Instance.Format("wpf.type.status.deletefailed", ex.Message);
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
        Status = LocaleManager.Instance.Format("wpf.type.status.loading", ns);
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
                "wpf.type.status.navfailed",
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
                "wpf.type.status.navfailed",
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
        return index > 0 ? ns[..index] : DefaultNamespace;
    }

    private bool ConfirmDelete(string fullName)
        => MessageBox.Show(
               LocaleManager.Instance.Format("wpf.type.status.confirmdelete", fullName),
               LocaleManager.Instance.Get("wpf.type.confirm.caption"),
               MessageBoxButton.YesNo,
               MessageBoxImage.Question) == MessageBoxResult.Yes;

    private void Teardown()
    {
        DisposeNamespaceControl();
        _session?.Dispose();
        _session = null;
        _loadVersion++;
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

    private sealed record NamespaceControl(IValueAccess? Node, DataNodeWrapper? Wrapper, Exception? Error);
}
