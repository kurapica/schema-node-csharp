using System.Text.Json.Nodes;
using System.Windows.Input;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using Prism.Navigation.Regions;
using SchemaNode.Context;
using SchemaNode.Data;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.WPF.AppView;
using SchemaNode.WPF.Man.Services;
using SchemaNode.WPF.View;
using SchemaNode.WPF.View.Localization;
using DecimalType = SchemaNode.Runtime.DecimalType;
using IntType = SchemaNode.Runtime.IntType;

namespace SchemaNode.WPF.Man.ViewModels;

/// <summary>
/// App field demo: resolves one <c>AppFieldType</c> and renders its data.
///
/// Where the pageable flag lives: C# Core has no PageNode, so there is no node kind to inspect.
/// <c>Pageable</c> is a property of <c>AppFieldType</c> (the field's presentation definition). The
/// screen therefore reads <see cref="AppFieldType.Pageable"/> on the field type, attaches the host
/// page source to the array node it created, and lets the registry pick the page table view for
/// that node. A non-pageable field is not bound, so the same node falls back to the plain table —
/// the screen never branches on a node kind.
///
/// The demo also seeds rows through the host data provider, because Man local mode's
/// <c>InMemoryAppDataProvider</c> starts empty: without seeding, a real pageable field shows
/// "no data" and no pager.
/// </summary>
public class AppFieldDemoViewModel : BindableBase, INavigationAware, IDestructible
{
    /// <summary>Navigation parameter carrying the owning app's full name.</summary>
    public const string AppParameter = "app";

    /// <summary>Navigation parameter carrying the field name.</summary>
    public const string FieldParameter = "field";

    /// <summary>Rows written by the demo seeder; large enough to produce several pages.</summary>
    private const int SeedRowCount = 12;

    private readonly SchemaContextFactory _factory;
    private readonly SchemaViewRegistry _registry;

    private SchemaScreenSession? _session;
    private AppFieldType? _field;
    private IValueAccess? _previewNode;

    private string _appName = string.Empty;
    private string _fieldName = string.Empty;
    private int _pageSize = 5;
    private string? _status;
    private bool _isBusy;
    private bool _isPageable;

    public AppFieldDemoViewModel(SchemaContextFactory factory, SchemaViewRegistry registry)
    {
        _factory = factory;
        _registry = registry;

        LoadCommand = new DelegateCommand(ExecuteLoad, () => !IsBusy && !string.IsNullOrWhiteSpace(AppName) && !string.IsNullOrWhiteSpace(FieldName))
            .ObservesProperty(() => AppName)
            .ObservesProperty(() => FieldName)
            .ObservesProperty(() => IsBusy);
        SeedCommand = new DelegateCommand(ExecuteSeed, () => !IsBusy && _field is not null)
            .ObservesProperty(() => PreviewNode)
            .ObservesProperty(() => IsBusy);
    }

    /// <summary>Application full name holding the field.</summary>
    public string AppName
    {
        get => _appName;
        set => SetProperty(ref _appName, value);
    }

    /// <summary>Field name inside the application.</summary>
    public string FieldName
    {
        get => _fieldName;
        set => SetProperty(ref _fieldName, value);
    }

    /// <summary>Rows requested from the host per page.</summary>
    public int PageSize
    {
        get => _pageSize;
        set
        {
            int clamped = Math.Clamp(value, 1, 100);
            SetProperty(ref _pageSize, clamped);
        }
    }

    public string? Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    /// <summary>Whether the resolved field declared <c>Pageable = true</c>.</summary>
    public bool IsPageable
    {
        get => _isPageable;
        private set => SetProperty(ref _isPageable, value);
    }

    /// <summary>Which presentation the node currently resolves to.</summary>
    public string PresentationText => IsPageable
        ? LocaleManager.Instance.Get("wpf.appfield.presentation.pageable")
        : LocaleManager.Instance.Get("wpf.appfield.presentation.plain");

    /// <summary>Adapter notes shown under the input area.</summary>
    public string HintText => LocaleManager.Instance.Get("wpf.appfield.hint.man");

    /// <summary>Root node rendered by the schema view; the registry decides which surface it is.</summary>
    public IValueAccess? PreviewNode
    {
        get => _previewNode;
        private set => SetProperty(ref _previewNode, value);
    }

    public SchemaViewRegistry Registry => _registry;

    public ICommand LoadCommand { get; }

    /// <summary>Seed command; typed so its enabled state can follow the loaded field.</summary>
    public DelegateCommand SeedCommand { get; }

    #region Navigation

    public bool IsNavigationTarget(NavigationContext navigationContext) => false;

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        Teardown();
        _session = _factory.CreateSession();

        // Opened from the field row's demo action: the app + field identities arrive as parameters.
        string app = navigationContext.Parameters.GetValue<string>(AppParameter) ?? string.Empty;
        string field = navigationContext.Parameters.GetValue<string>(FieldParameter) ?? string.Empty;

        if (app.Length > 0) AppName = app;
        if (field.Length > 0) FieldName = field;

        Status = LocaleManager.Instance.Get("wpf.appfield.status.ready");

        if (app.Length > 0 && field.Length > 0)
            ExecuteLoad();
    }

    public void OnNavigatedFrom(NavigationContext navigationContext) => Teardown();

    public void Destroy() => Teardown();

    private void Teardown()
    {
        ClearPreview();
        _session?.Dispose();
        _session = null;
        Status = null;
        IsBusy = false;
        IsPageable = false;
    }

    #endregion

    #region Load

    private async void ExecuteLoad()
    {
        if (_session is null) return;

        IsBusy = true;
        try
        {
            string appName = AppName.Trim();
            string fieldName = FieldName.Trim();

            AppType? app = await _session.Context.GetAppTypeAsync(appName);
            if (app is null)
            {
                Status = LocaleManager.Instance.Format("wpf.appfield.status.appnotfound", appName);
                return;
            }

            AppFieldType? field = app.GetField(fieldName);
            if (field is null)
            {
                Status = LocaleManager.Instance.Format("wpf.appfield.status.fieldnotfound", fieldName, appName);
                return;
            }

            if (field.Error is not null)
            {
                Status = LocaleManager.Instance.Format("wpf.appfield.status.fielderror", field.Error);
                return;
            }

            await ShowFieldAsync(field);
        }
        catch (Exception ex)
        {
            Status = LocaleManager.Instance.Format("wpf.appfield.status.loadfailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Creates a fresh array node for the field and, when the field is pageable, attaches the host
    /// page source. Binding before assigning <see cref="PreviewNode"/> matters: the view resolves
    /// its template from the registry when the node is first presented.
    /// </summary>
    private async Task ShowFieldAsync(AppFieldType field)
    {
        if (_session is null) return;

        ClearPreview();

        SchemaNode.Runtime.ValueType? valueType = field.ValueType;
        if (valueType is not ArrayType arrayType)
        {
            Status = LocaleManager.Instance.Format("wpf.appfield.status.notarray", field.Name);
            return;
        }

        IValueAccess? node = arrayType.Create(_session.Context);
        if (node is not ArrayNode array || node is not DataNode dataNode)
        {
            (node as IDisposable)?.Dispose();
            Status = LocaleManager.Instance.Format("wpf.appfield.status.createfailed", field.Name);
            return;
        }

        _field = field;
        _session.TrackNode(array);

        IsPageable = field.Pageable == true;
        if (IsPageable)
        {
            ManAppFieldDataAdapter adapter = new(_session.Context, field, PageSize);
            PageTableSchemaViews.Bind(dataNode, _registry, adapter, field.Filters);
            RaisePropertyChanged(nameof(PresentationText));
        }
        else
        {
            RaisePropertyChanged(nameof(PresentationText));
        }

        PreviewNode = array;
        SeedCommand.RaiseCanExecuteChanged();

        Status = IsPageable
            ? LocaleManager.Instance.Format("wpf.appfield.status.pageable", field.Name, PageSize)
            : LocaleManager.Instance.Format("wpf.appfield.status.plain", field.Name);
    }

    #endregion

    #region Seed

    /// <summary>
    /// Writes demo rows through the host provider so the pager has more than one page. The row
    /// shape is minimal: only the primary columns are filled, because the seed must work for any
    /// struct shape the selected field declares.
    /// </summary>
    private async void ExecuteSeed()
    {
        if (_session is null || _field is null) return;
        if (!_field.EnableStorage)
        {
            Status = LocaleManager.Instance.Get("wpf.appfield.status.seed.storage");
            return;
        }
        if (_field.ValueType is not ArrayType { Primary.Count: > 0 } arrayType)
        {
            Status = LocaleManager.Instance.Get("wpf.appfield.status.seed.noprimary");
            return;
        }

        IsBusy = true;
        try
        {
            JsonArray rows = new();
            for (int i = 1; i <= SeedRowCount; i++)
            {
                JsonObject row = new();
                foreach (string primary in arrayType.Primary)
                {
                    StructFieldType? primaryField = arrayType.Element is StructType element
                        ? element.GetField(primary)
                        : null;
                    row[primary] = primaryField?.Type is IntType or DecimalType
                        ? JsonValue.Create(i)
                        : JsonValue.Create($"row-{i}");
                }
                rows.Add(row);
            }

            bool saved = await _session.Context.SaveFieldDataAsync(_field, rows);
            if (!saved)
            {
                Status = LocaleManager.Instance.Get("wpf.appfield.status.seed.failed");
                return;
            }

            await ShowFieldAsync(_field);
            Status = LocaleManager.Instance.Format("wpf.appfield.status.seed.done", SeedRowCount, _field.Name);
        }
        catch (Exception ex)
        {
            Status = LocaleManager.Instance.Format("wpf.appfield.status.seed.error", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion

    #region Helpers

    private void ClearPreview()
    {
        IValueAccess? node = _previewNode;
        PreviewNode = null;
        _field = null;
        IsPageable = false;
        RaisePropertyChanged(nameof(PresentationText));
        SeedCommand.RaiseCanExecuteChanged();

        if (node is not IDisposable disposable) return;
        if (_session is null)
            disposable.Dispose();
        else
            _session.ReleaseNode(disposable);
    }

    #endregion
}
