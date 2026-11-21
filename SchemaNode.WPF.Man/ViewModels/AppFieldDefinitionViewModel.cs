using System.Windows.Input;
using System.Text.Json.Nodes;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using Prism.Navigation.Regions;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property.Common;
using SchemaNode.Schema;
using SchemaNode.Schema.Provider;
using SchemaNode.WPF.Man.Services;
using SchemaNode.WPF.Man.Views;
using SchemaNode.WPF.View;
using SchemaNode.WPF.View.Localization;
using SchemaNode.WPF.View.NodeWrapper;
using StructType = SchemaNode.Runtime.StructType;
using IValueAccess = SchemaNode.Runtime.IValueAccess;
using static SchemaNode.Utility.AppConstant;

namespace SchemaNode.WPF.Man.ViewModels;

/// <summary>
/// Field definition screen (view / edit / new) for one app field.
///
/// Opened from the field list. The app and field identities are supplied through Prism navigation
/// parameters. The editor is a SchemaView over the Attach-driven
/// <c>system.schema.app.field.schema</c> type, exactly like the app definition screen, so every
/// property group comes from Attach metadata rather than a hand-written field list.
/// </summary>
public class AppFieldDefinitionViewModel : BindableBase, INavigationAware, IDestructible
{
    public const string AppParameter = "app";
    public const string FieldParameter = "field";
    public const string ModeParameter = "mode";

    public const string ModeView = "view";
    public const string ModeEdit = "edit";
    public const string ModeNew = "new";

    private const string MainContentRegion = "MainContentRegion";
    private const string FieldSchemaTypeName = $"{NS_SYSTEM_SCHEMA_APP_FIELD}.schema";

    private readonly SchemaContextFactory _factory;
    private readonly SchemaViewRegistry _registry;
    private readonly IRegionManager _regionManager;

    private SchemaScreenSession? _session;
    private string _app = string.Empty;
    private string _fieldName = string.Empty;
    private string _mode = ModeEdit;
    private string? _status;
    private bool _isBusy;
    private bool _isNavigating;
    private IValueAccess? _fieldNode;

    public AppFieldDefinitionViewModel(
        SchemaContextFactory factory,
        SchemaViewRegistry registry,
        IRegionManager regionManager)
    {
        _factory = factory;
        _registry = registry;
        _regionManager = regionManager;

        SaveCommand = new DelegateCommand(ExecuteSave, () => FieldNode is not null && IsEditable && !IsBusy)
            .ObservesProperty(() => FieldNode)
            .ObservesProperty(() => IsBusy)
            .ObservesProperty(() => IsEditable);
        BackCommand = new DelegateCommand(ExecuteBack);
    }

    #region Bindable state

    /// <summary>Full name of the owning app.</summary>
    public string AppName => _app;

    /// <summary>Field name (empty while creating a new field).</summary>
    public string FieldName => _fieldName;

    /// <summary>Current document mode (view / edit / new).</summary>
    public string Mode => _mode;

    /// <summary>Whether the editor accepts input.</summary>
    public bool IsEditable => _mode is ModeEdit or ModeNew;

    /// <summary>Localized header describing the mode and subject.</summary>
    public string HeaderText => _mode switch
    {
        ModeNew => LocaleManager.Instance.Format("wpf.appfielddef.header.new", _app),
        ModeView => LocaleManager.Instance.Format("wpf.appfielddef.header.view", _fieldName),
        _ => LocaleManager.Instance.Format("wpf.appfielddef.header.edit", _fieldName),
    };

    /// <summary>SchemaView node carrying the Attach-driven field properties.</summary>
    public DataNodeWrapper? FieldNode { get; private set; }

    public SchemaViewRegistry Registry => _registry;

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

    public ICommand SaveCommand { get; }
    public ICommand BackCommand { get; }

    #endregion

    #region Navigation

    public bool IsNavigationTarget(NavigationContext navigationContext) => false;

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        Teardown();
        try
        {
            _session = _factory.CreateSession();
            _app = navigationContext.Parameters.GetValue<string>(AppParameter) ?? string.Empty;
            _fieldName = navigationContext.Parameters.GetValue<string>(FieldParameter) ?? string.Empty;
            string? mode = navigationContext.Parameters.GetValue<string>(ModeParameter);
            _mode = mode is ModeView or ModeEdit or ModeNew ? mode : ModeEdit;

            RaisePropertyChanged(nameof(AppName));
            RaisePropertyChanged(nameof(FieldName));
            RaisePropertyChanged(nameof(Mode));
            RaisePropertyChanged(nameof(IsEditable));
            RaisePropertyChanged(nameof(HeaderText));

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
        if (_session is null) return;
        if (string.IsNullOrWhiteSpace(_app))
        {
            Status = LocaleManager.Instance.Get("wpf.appfielddef.status.noname");
            return;
        }

        IsBusy = true;
        Status = _mode == ModeNew
            ? LocaleManager.Instance.Get("wpf.appfielddef.status.newready")
            : LocaleManager.Instance.Format("wpf.appfielddef.status.loading", _fieldName);
        try
        {
            AppFieldSchema? schema = null;
            if (_mode != ModeNew)
            {
                AppSchema? appSchema = await _session.Api.GetAppSchemaAsync(_app, includeTypes: true);
                schema = appSchema?.Fields?.FirstOrDefault(
                    field => field.Name.Equals(_fieldName, StringComparison.Ordinal));
                if (schema is null)
                {
                    Status = LocaleManager.Instance.Format("wpf.appfielddef.status.notfound", _fieldName);
                    return;
                }
            }

            StructType? type = await _session.Context.GetNodeTypeAsync<StructType>(FieldSchemaTypeName);
            if (type is null)
            {
                Status = LocaleManager.Instance.Get("wpf.appfielddef.status.notype");
                return;
            }

            JsonObject seed = BuildSeed(schema, _app, _fieldName);
            IValueAccess? node = type.Create(_session.Context, seed);
            if (node is null)
            {
                Status = LocaleManager.Instance.Format("wpf.appfielddef.status.error", FieldSchemaTypeName);
                return;
            }

            FieldNode = NodeWrapperFactory.Create(node, _registry);
            _fieldNode = node;
            if (node is IDisposable disposable)
                _session.TrackNode(disposable);

            // Name is the primary index of the field; it cannot be renamed in place.
            if (_mode != ModeNew && node.GetAccessValue("name") is { } nameField)
                nameField.SetPropertyValue<ReadOnly, bool>(true, null, null);

            if (_mode != ModeNew)
            {
                _fieldName = schema!.Name;
                RaisePropertyChanged(nameof(FieldName));
                RaisePropertyChanged(nameof(HeaderText));
                Status = LocaleManager.Instance.Format("wpf.appfielddef.status.loaded", _fieldName);
            }
        }
        catch (Exception ex)
        {
            Status = LocaleManager.Instance.Format("wpf.appfielddef.status.error", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    #region Commands

    private async void ExecuteSave()
    {
        SchemaScreenSession? session = _session;
        if (session is null || _fieldNode is null || !IsEditable) return;

        IsBusy = true;
        try
        {
            IValueAccess node = _fieldNode;
            string? name = node.GetAccessValue("name")?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(name))
            {
                Status = LocaleManager.Instance.Get("wpf.appfielddef.status.noname");
                return;
            }

            string? fieldType = node.GetAccessValue("type")?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(fieldType))
            {
                Status = LocaleManager.Instance.Get("wpf.appfielddef.status.typeempty");
                return;
            }

            AppFieldSchema? field = node.GetValue<AppFieldSchema>();
            if (field is null)
            {
                Status = LocaleManager.Instance.Format("wpf.appfielddef.status.error", name);
                return;
            }

            field.App = _app;
            field.Name = name.Trim();
            field.Type = fieldType;

            bool saved = await Task.Run(() => session.Context.SaveAppFieldSchemaAsync(_app, field));
            if (!saved)
            {
                Status = LocaleManager.Instance.Format("wpf.appfielddef.status.savefailed", field.Name);
                return;
            }

            // Saved field is now an existing one: switch to edit mode and lock the name.
            _fieldName = field.Name;
            _mode = ModeEdit;
            RaisePropertyChanged(nameof(FieldName));
            RaisePropertyChanged(nameof(Mode));
            RaisePropertyChanged(nameof(IsEditable));
            RaisePropertyChanged(nameof(HeaderText));
            node.GetAccessValue("name")?.SetPropertyValue<ReadOnly, bool>(true, null, null);
            Status = LocaleManager.Instance.Format("wpf.appfielddef.status.saved", field.Name);
        }
        catch (Exception ex)
        {
            Status = LocaleManager.Instance.Format("wpf.appfielddef.status.savefailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void ExecuteBack()
    {
        if (_isNavigating) return;
        _isNavigating = true;
        try
        {
            TaskCompletionSource<INavigationResult> completion = new();
            _regionManager.RequestNavigate(
                MainContentRegion,
                nameof(AppFieldListView),
                result => completion.TrySetResult(result),
                new NavigationParameters
                {
                    { AppFieldListViewModel.AppParameter, _app },
                });

            INavigationResult outcome = await completion.Task;
            if (!outcome.Success)
            {
                Status = LocaleManager.Instance.Format(
                    "wpf.appfielddef.status.navfailed",
                    outcome.Exception?.Message ?? nameof(AppFieldListView));
            }
        }
        finally
        {
            _isNavigating = false;
        }
    }

    #endregion

    private static JsonObject BuildSeed(AppFieldSchema? schema, string app, string name)
    {
        JsonObject seed = schema?.Extensions?.DeepClone() as JsonObject ?? new JsonObject();
        seed["app"] = app;
        if (!string.IsNullOrWhiteSpace(name))
            seed["name"] = name;
        if (!string.IsNullOrWhiteSpace(schema?.Type))
            seed["type"] = schema!.Type;
        return seed;
    }

    private void Teardown()
    {
        DataNodeWrapper? wrapper = FieldNode;
        FieldNode = null;
        wrapper?.Dispose();

        IValueAccess? node = _fieldNode;
        _fieldNode = null;
        if (node is IDisposable disposable)
        {
            if (_session is null)
                disposable.Dispose();
            else
                _session.ReleaseNode(disposable);
        }

        _session?.Dispose();
        _session = null;
        _app = string.Empty;
        _fieldName = string.Empty;
        _mode = ModeEdit;
        _isNavigating = false;
        Status = null;
        IsBusy = false;
    }
}
