using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using Prism.Navigation.Regions;
using SchemaNode.Context;
using SchemaNode.Property.Common;
using SchemaNode.Schema;
using SchemaNode.Schema.Provider;
using SchemaNode.WPF.Man.Services;
using SchemaNode.WPF.Man.Views;
using SchemaNode.WPF.View;
using SchemaNode.WPF.View.Localization;

namespace SchemaNode.WPF.Man.ViewModels;

/// <summary>
/// Workflow list of one app, opened from the app row's "工作流" action (only for apps with Fields).
///
/// Mirror of the workflow drawer in <c>appSchema.vue</c>: view / edit / activate-toggle / delete.
/// The app identity is a Prism navigation parameter; the selected workflow travels to the
/// configuration screen as an additional parameter.
/// </summary>
public class AppWorkflowListViewModel : BindableBase, INavigationAware, IDestructible
{
    /// <summary>Navigation parameter carrying the full app name (required).</summary>
    public const string AppParameter = "app";

    private const string MainContentRegion = "MainContentRegion";

    private readonly SchemaContextFactory _factory;
    private readonly IRegionManager _regionManager;

    private SchemaScreenSession? _session;
    private string _appFullName = string.Empty;
    private string _container = string.Empty;
    private string? _status;
    private bool _isBusy;
    private bool _isNavigating;
    private int _loadVersion;

    public AppWorkflowListViewModel(SchemaContextFactory factory, IRegionManager regionManager)
    {
        _factory = factory;
        _regionManager = regionManager;

        Workflows = new ObservableCollection<AppWorkflowSchemaRowViewModel>();

        NewCommand = new DelegateCommand(ExecuteNew, () => !IsBusy)
            .ObservesProperty(() => IsBusy);
        RefreshCommand = new DelegateCommand(ExecuteRefresh, () => !string.IsNullOrWhiteSpace(AppFullName) && !IsBusy)
            .ObservesProperty(() => AppFullName)
            .ObservesProperty(() => IsBusy);
        BackCommand = new DelegateCommand(ExecuteBack);
        ViewCommand = new DelegateCommand<AppWorkflowSchemaRowViewModel>(row => NavigateWorkflow(row, WorkflowConfigViewModel.ModeView));
        EditCommand = new DelegateCommand<AppWorkflowSchemaRowViewModel>(row => NavigateWorkflow(row, WorkflowConfigViewModel.ModeEdit));
        ToggleCommand = new DelegateCommand<AppWorkflowSchemaRowViewModel>(ExecuteToggle);
        DeleteCommand = new DelegateCommand<AppWorkflowSchemaRowViewModel>(ExecuteDelete);
    }

    #region Bindable state

    public string AppFullName
    {
        get => _appFullName;
        private set => SetProperty(ref _appFullName, value);
    }

    public ObservableCollection<AppWorkflowSchemaRowViewModel> Workflows { get; }

    public bool IsEmpty => Workflows.Count == 0;

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
    public ICommand BackCommand { get; }
    public ICommand ViewCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand ToggleCommand { get; }
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
            AppFullName = navigationContext.Parameters.GetValue<string>(AppParameter) ?? string.Empty;
            _ = InitializeAsync();
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    public void OnNavigatedFrom(NavigationContext navigationContext) => Teardown();

    public void Destroy() => Teardown();

    #endregion

    private async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(AppFullName))
        {
            Status = LocaleManager.Instance.Get("wpf.appworkflowlist.status.notfound");
            return;
        }

        IsBusy = true;
        Status = LocaleManager.Instance.Format("wpf.appworkflowlist.status.loading", AppFullName);
        try
        {
            await LoadWorkflowsAsync();
        }
        catch (Exception ex)
        {
            Status = LocaleManager.Instance.Format("wpf.appworkflowlist.status.loadfailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadWorkflowsAsync()
    {
        SchemaScreenSession? session = _session;
        if (session is null) return;

        int version = ++_loadVersion;
        string app = AppFullName;
        (AppWorkflowSchemaRowViewModel[] rows, string container, bool found) = await Task.Run(async () =>
        {
            AppSchema? schema = await session.Api.GetAppSchemaAsync(app, includeTypes: true);
            if (schema is null) return (Array.Empty<AppWorkflowSchemaRowViewModel>(), string.Empty, false);

            AppWorkflowSchema[] workflows = schema.Workflows ?? [];
            Array.Sort(workflows, (left, right) =>
            {
                int order = left.Seqno.CompareTo(right.Seqno);
                return order != 0 ? order : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            });

            AppWorkflowSchemaRowViewModel[] list = new AppWorkflowSchemaRowViewModel[workflows.Length];
            for (int index = 0; index < workflows.Length; index++)
            {
                AppWorkflowSchema workflow = workflows[index];
                string display = workflow.GetProperty<Display>()?.Value?.ToString(session.Context) ?? workflow.Name;
                string description = workflow.GetProperty<Description>()?.Value?.ToString(session.Context) ?? string.Empty;
                list[index] = new AppWorkflowSchemaRowViewModel(workflow, display, description);
            }

            return (list, schema.Container ?? string.Empty, true);
        });

        if (version != _loadVersion) return;

        if (!found)
        {
            Workflows.Clear();
            RaisePropertyChanged(nameof(IsEmpty));
            Status = LocaleManager.Instance.Format("wpf.appworkflowlist.status.notfound", app);
            return;
        }

        _container = container;
        Workflows.Clear();
        foreach (AppWorkflowSchemaRowViewModel row in rows)
            Workflows.Add(row);
        RaisePropertyChanged(nameof(IsEmpty));

        Status = rows.Length == 0
            ? LocaleManager.Instance.Get("wpf.appworkflowlist.status.empty")
            : LocaleManager.Instance.Format("wpf.appworkflowlist.status.loaded", rows.Length);
    }

    #region Commands

    private async void ExecuteNew()
    {
        try
        {
            await NavigateAsync(
                nameof(WorkflowConfigView),
                new NavigationParameters
                {
                    { WorkflowConfigViewModel.AppParameter, AppFullName },
                    { WorkflowConfigViewModel.ModeParameter, WorkflowConfigViewModel.ModeNew },
                },
                "wpf.appworkflowlist.status.navfailed");
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
            IsBusy = true;
            await LoadWorkflowsAsync();
        }
        catch (Exception ex)
        {
            Status = LocaleManager.Instance.Format("wpf.appworkflowlist.status.loadfailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void ExecuteBack()
    {
        await NavigateAsync(
            nameof(AppListView),
            new NavigationParameters
            {
                { AppListViewModel.NamespaceParameter, _container },
            },
            "wpf.appworkflowlist.status.navfailed");
    }

    private async void NavigateWorkflow(AppWorkflowSchemaRowViewModel? row, string mode)
    {
        if (row is null) return;
        try
        {
            await NavigateAsync(
                nameof(WorkflowConfigView),
                new NavigationParameters
                {
                    { WorkflowConfigViewModel.AppParameter, AppFullName },
                    { WorkflowConfigViewModel.WorkflowParameter, row.Name },
                    { WorkflowConfigViewModel.ModeParameter, mode },
                },
                "wpf.appworkflowlist.status.navfailed");
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    private async void ExecuteToggle(AppWorkflowSchemaRowViewModel? row)
    {
        SchemaScreenSession? session = _session;
        if (row is null || session is null) return;

        IsBusy = true;
        try
        {
            bool active = !row.Active;
            bool changed = await Task.Run(
                () => session.Context.ToggleAppWorkflowSchemaAsync(AppFullName, row.Name, active));
            if (!changed)
            {
                Status = LocaleManager.Instance.Format("wpf.appworkflowlist.status.togglefailed", row.Name);
                return;
            }

            await LoadWorkflowsAsync();
            Status = LocaleManager.Instance.Format(
                active ? "wpf.appworkflowlist.status.activated" : "wpf.appworkflowlist.status.deactivated",
                row.Name);
        }
        catch (Exception ex)
        {
            Status = LocaleManager.Instance.Format("wpf.appworkflowlist.status.togglefailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void ExecuteDelete(AppWorkflowSchemaRowViewModel? row)
    {
        SchemaScreenSession? session = _session;
        if (row is null || session is null || !row.CanDelete) return;
        if (!ConfirmDelete(row.Name)) return;

        IsBusy = true;
        try
        {
            bool removed = await Task.Run(
                () => session.Api.DeleteAppWorkflowSchemaAsync(AppFullName, row.Name));
            if (!removed)
            {
                Status = LocaleManager.Instance.Format("wpf.appworkflowlist.status.deletefailed", row.Name);
                return;
            }

            await LoadWorkflowsAsync();
            Status = LocaleManager.Instance.Format("wpf.appworkflowlist.status.deleted", row.Name);
        }
        catch (Exception ex)
        {
            Status = LocaleManager.Instance.Format("wpf.appworkflowlist.status.deletefailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion

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

    private bool ConfirmDelete(string workflowName)
        => MessageBox.Show(
               LocaleManager.Instance.Format("wpf.appworkflowlist.status.confirmdelete", workflowName),
               LocaleManager.Instance.Get("wpf.type.confirm.caption"),
               MessageBoxButton.YesNo,
               MessageBoxImage.Question) == MessageBoxResult.Yes;

    private void Teardown()
    {
        _session?.Dispose();
        _session = null;
        _loadVersion++;
        Workflows.Clear();
        _container = string.Empty;
        _isNavigating = false;
        Status = null;
        IsBusy = false;
    }
}
