using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using SchemaNode.Avalonia.Man.Infrastructure;
using SchemaNode.Avalonia.Man.Navigation;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property.Common;
using SchemaNode.Schema;
using SchemaNode.Avalonia.Man.Services;
using SchemaNode.Avalonia.Man.Views;
using SchemaNode.Avalonia.View;
using SchemaNode.Avalonia.View.Localization;
using SchemaNode.Avalonia.View.NodeWrapper;
using AppType = SchemaNode.Runtime.AppType;
using IValueAccess = SchemaNode.Runtime.IValueAccess;
using IValueTypeAccess = SchemaNode.Runtime.IValueTypeAccess;
using static SchemaNode.Utility.AppConstant;

namespace SchemaNode.Avalonia.Man.ViewModels;

/// <summary>
/// "App" list screen (administrator entry).
///
/// Mirrors <c>schema-node-man/src/components/appSchema.vue</c>:
///   - the app namespace is a SchemaView over a <c>system.schema.app.type</c> node, so child apps
///     are discovered through the Core entry source (cascading selection) instead of free text;
///   - every row is one direct child app of the selected namespace;
///   - the row's available actions follow the vue predicates: drill only without Fields, field list
///     only without sub apps, workflow only with Fields, delete only for a leaf app;
///   - field / workflow identities travel through Prism navigation parameters; no static state is
///     shared between screens.
/// </summary>
public class AppListViewModel : BindableBase, INavigationAware, IDestructible
{
    /// <summary>Navigation parameter carrying the app namespace to list (empty means the root app).</summary>
    public const string NamespaceParameter = "app";

    private const string AppTypeName = $"{NS_SYSTEM_SCHEMA_APP}.type";
    private const string MainContentRegion = "MainContentRegion";

    private readonly SchemaContextFactory _factory;
    private readonly IRegionManager _regionManager;
    private readonly SchemaViewRegistry _registry;
    private readonly DialogService _dialogs;

    private SchemaScreenSession? _session;
    private IValueAccess? _appTypeNode;
    private DataNodeWrapper? _appTypeWrapper;
    private string _appNamespace = string.Empty;
    private string? _status;
    private bool _isBusy;
    private bool _isNavigating;
    private int _loadVersion;

    public AppListViewModel(SchemaContextFactory factory, IRegionManager regionManager, SchemaViewRegistry registry, DialogService dialogs)
    {
        _factory = factory;
        _regionManager = regionManager;
        _registry = registry;
        _dialogs = dialogs;

        Apps = new ObservableCollection<AppSchemaRowViewModel>();

        NewCommand = new DelegateCommand(ExecuteNew);
        RefreshCommand = new DelegateCommand(ExecuteRefresh);
        UpCommand = new DelegateCommand(ExecuteUp, () => CanGoUp);
        ViewCommand = new DelegateCommand<AppSchemaRowViewModel>(ExecuteView);
        EditCommand = new DelegateCommand<AppSchemaRowViewModel>(ExecuteEdit);
        EnterCommand = new DelegateCommand<AppSchemaRowViewModel>(ExecuteEnter);
        FieldsCommand = new DelegateCommand<AppSchemaRowViewModel>(ExecuteFields);
        WorkflowCommand = new DelegateCommand<AppSchemaRowViewModel>(ExecuteWorkflow);
        DeleteCommand = new DelegateCommand<AppSchemaRowViewModel>(ExecuteDelete);
    }

    #region Bindable state

    /// <summary>App namespace selector rendered through SchemaView.</summary>
    public DataNodeWrapper? AppTypeWrapper
    {
        get => _appTypeWrapper;
        private set => SetProperty(ref _appTypeWrapper, value);
    }

    /// <summary>Current app namespace (empty means the root app).</summary>
    public string AppNamespace
    {
        get => _appNamespace;
        private set
        {
            if (!SetProperty(ref _appNamespace, value)) return;
            RaisePropertyChanged(nameof(CanGoUp));
            (UpCommand as DelegateCommand)?.RaiseCanExecuteChanged();
        }
    }

    /// <summary>Root has no parent; any non-empty namespace does.</summary>
    public bool CanGoUp => _appNamespace.Length > 0;

    /// <summary>Direct child apps of the current namespace.</summary>
    public ObservableCollection<AppSchemaRowViewModel> Apps { get; }

    /// <summary>Whether the list is empty (drives the empty-state hint).</summary>
    public bool IsEmpty => Apps.Count == 0;

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

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
    public ICommand FieldsCommand { get; }
    public ICommand WorkflowCommand { get; }
    public ICommand DeleteCommand { get; }

    #endregion

    #region Navigation

    public bool IsNavigationTarget(NavigationContext navigationContext) => false;

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        Teardown();

        try
        {
            _session = _factory.CreateSession();
            string start = navigationContext.Parameters.GetValue<string>(NamespaceParameter) ?? string.Empty;
            AppNamespace = start;
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
        Status = LocaleManager.Instance.Format("avalonia.applist.status.loading", DisplayNamespace(ns));
        try
        {
            await RebuildAppTypeControlAsync(ns);
            await LoadListAsync(ns);
        }
        catch (Exception ex)
        {
            Status = LocaleManager.Instance.Format("avalonia.applist.status.loadfailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Builds the cascading app-namespace selector on the thread pool.</summary>
    private async Task RebuildAppTypeControlAsync(string ns)
    {
        DisposeAppTypeControl();
        SchemaScreenSession? session = _session;
        if (session is null) return;

        SchemaViewRegistry registry = _registry;
        AppTypeControl built = await Task.Run(async () =>
        {
            IValueAccess? node = null;
            try
            {
                IValueTypeAccess? type = await session.Context
                    .GetNodeTypeAsync<IValueTypeAccess>(AppTypeName).ConfigureAwait(false);
                if (type is null) return new AppTypeControl(null, null, null);

                node = type.Create(session.Context, ns);
                if (node is null) return new AppTypeControl(null, null, null);

                DataNodeWrapper wrapper = NodeWrapperFactory.Create(node, registry);
                return new AppTypeControl(node, wrapper, null);
            }
            catch (Exception ex)
            {
                if (node is IDisposable disposable) disposable.Dispose();
                return new AppTypeControl(null, null, ex);
            }
        });

        if (built.Node is null)
        {
            Status = built.Error?.Message
                     ?? LocaleManager.Instance.Format("avalonia.applist.status.loadfailed", AppTypeName);
            return;
        }

        _appTypeNode = built.Node;
        built.Wrapper!.PropertyChanged += OnAppTypeWrapperChanged;
        AppTypeWrapper = built.Wrapper;
    }

    /// <summary>Cascading selection writes back into the app namespace node and reloads the list.</summary>
    private void OnAppTypeWrapperChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(InputWrapper.Value)) return;

        string? value = _appTypeNode?.GetValue<string>() ?? string.Empty;
        if (value.Equals(_appNamespace, StringComparison.Ordinal)) return;

        AppNamespace = value;
        _ = ReloadForNamespaceAsync(value);
    }

    private async Task ReloadForNamespaceAsync(string ns)
    {
        IsBusy = true;
        Status = LocaleManager.Instance.Format("avalonia.applist.status.loading", DisplayNamespace(ns));
        try
        {
            await LoadListAsync(ns);
        }
        catch (Exception ex)
        {
            Status = LocaleManager.Instance.Format("avalonia.applist.status.loadfailed", ex.Message);
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
        AppSchemaRowViewModel[] rows = await Task.Run(async () =>
        {
            // Reload the namespace type first so a screen opened after an edit sees the current
            // providers; the shared runtime caches app types between sessions.
            AppType? container = await session.Context.GetAppTypeAsync(ns, reload: true).ConfigureAwait(false);
            if (container is null) return [];

            AppSchema? schema = await session.Api.GetAppSchemaAsync(ns).ConfigureAwait(false);
            AppSchema[] apps = schema?.Apps ?? [];
            List<AppSchemaRowViewModel> list = new(apps.Length);
            foreach (AppSchema app in apps)
            {
                AppType? appType = await session.Context.GetAppTypeAsync(app.FullName).ConfigureAwait(false);
                bool hasFields = appType?.GetFields().Any() == true;
                bool hasApps = appType?.HasSubApps == true;
                bool hasWorkflows = appType?.GetWorkflows().Any() == true;
                string display = app.GetProperty<Display>()?.Value?.ToString(session.Context) ?? app.Name;
                list.Add(new AppSchemaRowViewModel(app, display, hasFields, hasApps, hasWorkflows));
            }

            return list
                .OrderBy(row => row.FullName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        });

        if (version != _loadVersion) return;

        Apps.Clear();
        foreach (AppSchemaRowViewModel row in rows)
            Apps.Add(row);
        RaisePropertyChanged(nameof(IsEmpty));

        Status = rows.Length == 0
            ? LocaleManager.Instance.Get("avalonia.applist.status.empty")
            : LocaleManager.Instance.Format("avalonia.applist.status.loaded", rows.Length);
    }

    #region Commands

    private async void ExecuteNew()
    {
        try
        {
            await NavigateAsync(
                nameof(AppDefinitionView),
                new NavigationParameters
                {
                    { AppDefinitionViewModel.ModeParameter, AppDefinitionViewModel.ModeNew },
                    { AppDefinitionViewModel.ContainerParameter, _appNamespace },
                },
                "avalonia.applist.status.navfailed");
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
            await ReloadForNamespaceAsync(_appNamespace);
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
            await SetNamespaceAsync(ParentOf(_appNamespace));
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    private async void ExecuteEnter(AppSchemaRowViewModel? row)
    {
        if (row is null || !row.CanDrill) return;
        try
        {
            await SetNamespaceAsync(row.FullName);
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    private async void ExecuteView(AppSchemaRowViewModel? row)
    {
        if (row is null) return;
        try
        {
            await NavigateAppAsync(row, AppDefinitionViewModel.ModeView);
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    private async void ExecuteEdit(AppSchemaRowViewModel? row)
    {
        if (row is null) return;
        try
        {
            await NavigateAppAsync(row, AppDefinitionViewModel.ModeEdit);
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    private async Task NavigateAppAsync(AppSchemaRowViewModel row, string mode)
    {
        await NavigateAsync(
            nameof(AppDefinitionView),
            new NavigationParameters
            {
                { AppDefinitionViewModel.ModeParameter, mode },
                { AppDefinitionViewModel.AppParameter, row.FullName },
                { AppDefinitionViewModel.ContainerParameter, _appNamespace },
            },
            "avalonia.applist.status.navfailed");
    }

    private async void ExecuteFields(AppSchemaRowViewModel? row)
    {
        if (row is null || !row.CanShowFields) return;
        try
        {
            await NavigateAsync(
                nameof(AppFieldListView),
                new NavigationParameters
                {
                    { AppFieldListViewModel.AppParameter, row.FullName },
                },
                "avalonia.applist.status.navfailed");
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    private async void ExecuteWorkflow(AppSchemaRowViewModel? row)
    {
        if (row is null || !row.CanShowWorkflow) return;
        try
        {
            await NavigateAsync(
                nameof(AppWorkflowListView),
                new NavigationParameters
                {
                    { AppWorkflowListViewModel.AppParameter, row.FullName },
                },
                "avalonia.applist.status.navfailed");
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    private async void ExecuteDelete(AppSchemaRowViewModel? row)
    {
        SchemaScreenSession? session = _session;
        if (row is null || session is null) return;

        if (row.IsSystemDefined)
        {
            Status = LocaleManager.Instance.Format("avalonia.applist.status.protected", row.FullName);
            return;
        }
        if (!row.CanDelete)
        {
            Status = LocaleManager.Instance.Format("avalonia.applist.status.inuse", row.FullName);
            return;
        }
        if (!await ConfirmDeleteAsync(row.FullName)) return;

        IsBusy = true;
        try
        {
            bool removed = await Task.Run(() => session.Api.DeleteAppSchemaAsync(row.FullName));
            if (!removed)
            {
                Status = LocaleManager.Instance.Format("avalonia.applist.status.deletefailed", row.FullName);
                return;
            }

            await LoadListAsync(_appNamespace);
            Status = LocaleManager.Instance.Format("avalonia.applist.status.deleted", row.FullName);
        }
        catch (Exception ex)
        {
            Status = LocaleManager.Instance.Format("avalonia.applist.status.deletefailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion

    private async Task SetNamespaceAsync(string ns)
    {
        AppNamespace = ns;
        IsBusy = true;
        Status = LocaleManager.Instance.Format("avalonia.applist.status.loading", DisplayNamespace(ns));
        try
        {
            await RebuildAppTypeControlAsync(ns);
            await LoadListAsync(ns);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Awaits the Prism navigation result so a missing route is reported instead of silently
    /// leaving the region unchanged (the shell navigation bug this restructure must not repeat).
    /// </summary>
    private async Task NavigateAsync(string viewName, NavigationParameters parameters, string failKey)
    {
        if (_isNavigating) return;
        _isNavigating = true;
        try
        {
            TaskCompletionSource<INavigationResult> completion = new();
            _regionManager.RequestNavigate(
                MainContentRegion,
                viewName,
                result => completion.TrySetResult(result),
                parameters);

            INavigationResult outcome = await completion.Task;
            if (outcome.Success) return;

            Status = LocaleManager.Instance.Format(
                failKey,
                outcome.Exception?.Message ?? viewName);
        }
        finally
        {
            _isNavigating = false;
        }
    }

    private static string ParentOf(string ns)
    {
        int index = ns.LastIndexOf('.');
        return index > 0 ? ns[..index] : string.Empty;
    }

    private static string DisplayNamespace(string ns)
        => ns.Length > 0 ? ns : LocaleManager.Instance.Get("avalonia.applist.namespace.root");

    private Task<bool> ConfirmDeleteAsync(string fullName)
        => _dialogs.ConfirmAsync(
            LocaleManager.Instance.Format("avalonia.applist.status.confirmdelete", fullName),
            LocaleManager.Instance.Get("avalonia.type.confirm.caption"));

    private void Teardown()
    {
        DisposeAppTypeControl();
        _session?.Dispose();
        _session = null;
        _loadVersion++;
        Apps.Clear();
        RaisePropertyChanged(nameof(IsEmpty));
    }

    /// <summary>Releases in reverse order: wrapper first (subscriptions), then the value node.</summary>
    private void DisposeAppTypeControl()
    {
        if (_appTypeWrapper is not null)
        {
            _appTypeWrapper.PropertyChanged -= OnAppTypeWrapperChanged;
            _appTypeWrapper.Dispose();
        }
        _appTypeWrapper = null;
        AppTypeWrapper = null;

        if (_appTypeNode is IDisposable disposable)
            _session?.ReleaseNode(disposable);
        _appTypeNode = null;
    }

    private sealed record AppTypeControl(IValueAccess? Node, DataNodeWrapper? Wrapper, Exception? Error);
}
