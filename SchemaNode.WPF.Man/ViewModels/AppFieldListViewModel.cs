using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using Prism.Navigation.Regions;
using SchemaNode.Context;
using SchemaNode.Property.App;
using SchemaNode.Property.Common;
using SchemaNode.Schema;
using SchemaNode.Schema.Provider;
using SchemaNode.WPF.Man.Services;
using SchemaNode.WPF.Man.Views;
using SchemaNode.WPF.View;
using SchemaNode.WPF.View.Localization;

namespace SchemaNode.WPF.Man.ViewModels;

/// <summary>
/// Field list of one app, opened from the app row's "字段" action.
///
/// Mirrors the field drawer of <c>appSchema.vue</c>: rows are ordered by Seqno, and the per-row
/// actions are view / edit / try-it / move-up / delete, gated by the Core-authored schema flags.
/// The app identity arrives through a Prism navigation parameter; nothing is read from static state.
/// </summary>
public class AppFieldListViewModel : BindableBase, INavigationAware, IDestructible
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
    private bool _isFieldAddable;
    private int _loadVersion;

    public AppFieldListViewModel(SchemaContextFactory factory, IRegionManager regionManager)
    {
        _factory = factory;
        _regionManager = regionManager;

        Fields = new ObservableCollection<AppFieldSchemaRowViewModel>();

        NewCommand = new DelegateCommand(ExecuteNew, () => IsFieldAddable && !IsBusy)
            .ObservesProperty(() => IsBusy)
            .ObservesProperty(() => IsFieldAddable);
        RefreshCommand = new DelegateCommand(ExecuteRefresh, () => !string.IsNullOrWhiteSpace(AppFullName) && !IsBusy)
            .ObservesProperty(() => AppFullName)
            .ObservesProperty(() => IsBusy);
        BackCommand = new DelegateCommand(ExecuteBack);
        ViewCommand = new DelegateCommand<AppFieldSchemaRowViewModel>(row => NavigateField(row, AppFieldDefinitionViewModel.ModeView));
        EditCommand = new DelegateCommand<AppFieldSchemaRowViewModel>(row => NavigateField(row, AppFieldDefinitionViewModel.ModeEdit));
        DemoCommand = new DelegateCommand<AppFieldSchemaRowViewModel>(ExecuteDemo);
        MoveUpCommand = new DelegateCommand<AppFieldSchemaRowViewModel>(ExecuteMoveUp);
        DeleteCommand = new DelegateCommand<AppFieldSchemaRowViewModel>(ExecuteDelete);
    }

    #region Bindable state

    /// <summary>Full name of the app whose fields are listed.</summary>
    public string AppFullName
    {
        get => _appFullName;
        private set => SetProperty(ref _appFullName, value);
    }

    public ObservableCollection<AppFieldSchemaRowViewModel> Fields { get; }

    public bool IsEmpty => Fields.Count == 0;

    /// <summary>Whether Core grants SchemaCreate on this app (controls "新建字段").</summary>
    public bool IsFieldAddable
    {
        get => _isFieldAddable;
        private set => SetProperty(ref _isFieldAddable, value);
    }

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
    public ICommand DemoCommand { get; }
    public ICommand MoveUpCommand { get; }
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
            Status = LocaleManager.Instance.Get("wpf.appfieldlist.status.notfound");
            return;
        }

        IsBusy = true;
        Status = LocaleManager.Instance.Format("wpf.appfieldlist.status.loading", AppFullName);
        try
        {
            await LoadFieldsAsync();
        }
        catch (Exception ex)
        {
            Status = LocaleManager.Instance.Format("wpf.appfieldlist.status.loadfailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadFieldsAsync()
    {
        SchemaScreenSession? session = _session;
        if (session is null) return;

        int version = ++_loadVersion;
        string app = AppFullName;
        (AppFieldSchemaRowViewModel[] rows, string container, bool addable, bool found) = await Task.Run(async () =>
        {
            AppSchema? schema = await session.Api.GetAppSchemaAsync(app, includeTypes: true);
            if (schema is null) return (Array.Empty<AppFieldSchemaRowViewModel>(), string.Empty, false, false);

            AppFieldSchema[] fields = schema.Fields ?? [];
            Array.Sort(fields, (left, right) =>
            {
                int order = left.Seqno.CompareTo(right.Seqno);
                return order != 0 ? order : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            });

            AppFieldSchemaRowViewModel[] list = new AppFieldSchemaRowViewModel[fields.Length];
            for (int index = 0; index < fields.Length; index++)
            {
                AppFieldSchema field = fields[index];
                string display = field.GetProperty<Display>()?.Value?.ToString(session.Context) ?? field.Name;
                list[index] = new AppFieldSchemaRowViewModel(field, display, index);
            }

            bool canCreate = schema.GetProperty<SchemaCreate>()?.Value == true;
            return (list, schema.Container ?? string.Empty, canCreate, true);
        });

        if (version != _loadVersion) return;

        if (!found)
        {
            Fields.Clear();
            RaisePropertyChanged(nameof(IsEmpty));
            Status = LocaleManager.Instance.Format("wpf.appfieldlist.status.notfound", app);
            return;
        }

        _container = container;
        IsFieldAddable = addable;
        Fields.Clear();
        foreach (AppFieldSchemaRowViewModel row in rows)
            Fields.Add(row);
        RaisePropertyChanged(nameof(IsEmpty));

        Status = rows.Length == 0
            ? LocaleManager.Instance.Get("wpf.appfieldlist.status.empty")
            : LocaleManager.Instance.Format("wpf.appfieldlist.status.loaded", rows.Length);
    }

    #region Commands

    private async void ExecuteNew()
    {
        if (!IsFieldAddable) return;
        try
        {
            await NavigateAsync(
                nameof(AppFieldDefinitionView),
                new NavigationParameters
                {
                    { AppFieldDefinitionViewModel.AppParameter, AppFullName },
                    { AppFieldDefinitionViewModel.ModeParameter, AppFieldDefinitionViewModel.ModeNew },
                },
                "wpf.appfieldlist.status.navfailed");
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
            await LoadFieldsAsync();
        }
        catch (Exception ex)
        {
            Status = LocaleManager.Instance.Format("wpf.appfieldlist.status.loadfailed", ex.Message);
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
            "wpf.appfieldlist.status.navfailed");
    }

    private async void NavigateField(AppFieldSchemaRowViewModel? row, string mode)
    {
        if (row is null) return;
        try
        {
            await NavigateAsync(
                nameof(AppFieldDefinitionView),
                new NavigationParameters
                {
                    { AppFieldDefinitionViewModel.AppParameter, AppFullName },
                    { AppFieldDefinitionViewModel.FieldParameter, row.Name },
                    { AppFieldDefinitionViewModel.ModeParameter, mode },
                },
                "wpf.appfieldlist.status.navfailed");
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    private async void ExecuteDemo(AppFieldSchemaRowViewModel? row)
    {
        if (row is null) return;
        try
        {
            await NavigateAsync(
                nameof(AppFieldDemoView),
                new NavigationParameters
                {
                    { AppFieldDemoViewModel.AppParameter, AppFullName },
                    { AppFieldDemoViewModel.FieldParameter, row.Name },
                },
                "wpf.appfieldlist.status.navfailed");
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    private async void ExecuteMoveUp(AppFieldSchemaRowViewModel? row)
    {
        SchemaScreenSession? session = _session;
        if (row is null || session is null || !row.CanMoveUp) return;

        AppFieldSchemaRowViewModel? previous = Fields.ElementAtOrDefault(row.Index - 1);
        if (previous is null) return;

        IsBusy = true;
        try
        {
            bool swapped = await Task.Run(
                () => session.Context.SwapAppFieldSchemaAsync(AppFullName, row.Name, previous.Name));
            if (!swapped)
            {
                Status = LocaleManager.Instance.Format("wpf.appfieldlist.status.movfailed", row.Name);
                return;
            }

            await LoadFieldsAsync();
            Status = LocaleManager.Instance.Format("wpf.appfieldlist.status.moved", row.Name);
        }
        catch (Exception ex)
        {
            Status = LocaleManager.Instance.Format("wpf.appfieldlist.status.movfailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void ExecuteDelete(AppFieldSchemaRowViewModel? row)
    {
        SchemaScreenSession? session = _session;
        if (row is null || session is null || !row.CanDelete) return;
        if (!ConfirmDelete(row.Name)) return;

        IsBusy = true;
        try
        {
            bool removed = await Task.Run(
                () => session.Context.DeleteAppFieldSchemaAsync(AppFullName, row.Name));
            if (!removed)
            {
                Status = LocaleManager.Instance.Format("wpf.appfieldlist.status.deletefailed", row.Name);
                return;
            }

            await LoadFieldsAsync();
            Status = LocaleManager.Instance.Format("wpf.appfieldlist.status.deleted", row.Name);
        }
        catch (Exception ex)
        {
            Status = LocaleManager.Instance.Format("wpf.appfieldlist.status.deletefailed", ex.Message);
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

    private bool ConfirmDelete(string fieldName)
        => MessageBox.Show(
               LocaleManager.Instance.Format("wpf.appfieldlist.status.confirmdelete", fieldName),
               LocaleManager.Instance.Get("wpf.type.confirm.caption"),
               MessageBoxButton.YesNo,
               MessageBoxImage.Question) == MessageBoxResult.Yes;

    private void Teardown()
    {
        _session?.Dispose();
        _session = null;
        _loadVersion++;
        Fields.Clear();
        _container = string.Empty;
        IsFieldAddable = false;
        _isNavigating = false;
        Status = null;
        IsBusy = false;
    }
}
