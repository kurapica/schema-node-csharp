using System.Text.Json.Nodes;
using System.Windows.Input;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using Prism.Navigation.Regions;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property.Common;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Schema.Provider;
using SchemaNode.WPF.Man.Services;
using SchemaNode.WPF.Man.Views;
using SchemaNode.WPF.View;
using SchemaNode.WPF.View.Localization;
using SchemaNode.WPF.View.NodeWrapper;
using static SchemaNode.Utility.AppConstant;
using static SchemaNode.Utility.Constant;
using ArrayType = SchemaNode.Runtime.ArrayType;
using StructType = SchemaNode.Runtime.StructType;

namespace SchemaNode.WPF.Man.ViewModels;

/// <summary>
/// App definition view model (screen-level).
///
/// Screen layout:
///   - The app property area directly renders the <c>system.schema.app.schema</c> definition
///     node. Fields of this type come from Core: <c>container</c> / <c>name</c> are fixed
///     AppSchema fields; Display / Description / Relations / SystemDefined / Auths /
///     ScopePolicy / Data* and so on are mounted dynamically by <c>[Meta&lt;Attach&gt;(app)]</c>
///     according to SchemaKind, so the view does not hard-code the property list.
///   - The App Fields area renders the whole field collection (one ArrayNode of
///     <c>system.schema.app.field.schema</c> definition nodes) through the registered
///     table template; <see cref="FieldsNode"/> is handed over raw and the view wraps it. Add /
///     remove rebuild the array node from JSON because Core's DelRows does not dispose
///     removed elements.
///
/// Lifecycle: navigation activation creates an isolated session (CreateSession already sets
/// FullMode). On leave, clear the bound nodes first so the views dispose their wrappers, then
/// release the field array node → app node → Context/Scope.
/// </summary>
public class AppDefinitionViewModel : BindableBase, INavigationAware, IDestructible
{
    public const string AppParameter = "app";
    public const string ModeParameter = "mode";
    public const string ContainerParameter = "container";

    public const string ModeView = "view";
    public const string ModeEdit = "edit";
    public const string ModeNew = "new";

    private const string MainContentRegion = "MainContentRegion";

    private readonly SchemaContextFactory _factory;
    private readonly SchemaViewRegistry _registry;
    private readonly IRegionManager _regionManager;
    private SchemaScreenSession? _session;

    private string _appName = string.Empty;
    private string _newFieldName = string.Empty;
    private string _newFieldType = NS_SYSTEM_STRING;
    private string? _status;
    private bool _isBusy;
    private bool _isNavigating;
    private string _mode = ModeEdit;
    private string _container = string.Empty;

    private IValueAccess? _appNode;
    private ArrayNode? _fieldsNode;

    /// <summary>Field names present when loaded, used on save to find removed fields (Core does not delete fields when saving an app).</summary>
    private readonly List<string> _originalFieldNames = [];

    public AppDefinitionViewModel(SchemaContextFactory factory, SchemaViewRegistry registry, IRegionManager regionManager)
    {
        _factory = factory;
        _registry = registry;
        _regionManager = regionManager;

        LoadCommand = new DelegateCommand(ExecuteLoad, () => !string.IsNullOrWhiteSpace(AppName) && !IsBusy)
            .ObservesProperty(() => AppName)
            .ObservesProperty(() => IsBusy);
        NewCommand = new DelegateCommand(ExecuteNew, () => IsEditable && !string.IsNullOrWhiteSpace(AppName) && !IsBusy)
            .ObservesProperty(() => AppName)
            .ObservesProperty(() => IsBusy)
            .ObservesProperty(() => IsEditable);
        SaveCommand = new DelegateCommand(ExecuteSave, () => AppNode is not null && IsEditable && !IsBusy)
            .ObservesProperty(() => AppNode)
            .ObservesProperty(() => IsBusy)
            .ObservesProperty(() => IsEditable);
        DeleteCommand = new DelegateCommand(ExecuteDelete, () => IsEditable && !string.IsNullOrWhiteSpace(AppName) && !IsBusy)
            .ObservesProperty(() => AppName)
            .ObservesProperty(() => IsBusy)
            .ObservesProperty(() => IsEditable);
        AddFieldCommand = new DelegateCommand(ExecuteAddField, () => IsEditable && _fieldsNode is not null && !IsBusy)
            .ObservesProperty(() => FieldsNode)
            .ObservesProperty(() => IsBusy)
            .ObservesProperty(() => IsEditable);
        RemoveFieldCommand = new DelegateCommand<DataNodeWrapper>(ExecuteRemoveField, _ => IsEditable && !IsBusy)
            .ObservesProperty(() => IsBusy)
            .ObservesProperty(() => IsEditable);
        BackCommand = new DelegateCommand(ExecuteBack);
    }

    /// <summary>Full app name (also used as the target full name when creating).</summary>
    public string AppName
    {
        get => _appName;
        set
        {
            if (!SetProperty(ref _appName, value)) return;
            RaisePropertyChanged(nameof(HeaderText));
        }
    }

    /// <summary>Current document mode (view / edit / new).</summary>
    public string Mode => _mode;

    /// <summary>Whether the property and field areas accept input (false for 查看).</summary>
    public bool IsEditable => _mode is ModeEdit or ModeNew;

    /// <summary>Header text describing the mode and subject.</summary>
    public string HeaderText => _mode switch
    {
        ModeNew => LocaleManager.Instance.Format(
            "wpf.appdef.header.new",
            _container.Length > 0 ? _container : LocaleManager.Instance.Get("wpf.applist.namespace.root")),
        ModeView => LocaleManager.Instance.Format("wpf.appdef.header.view", _appName),
        _ => LocaleManager.Instance.Format("wpf.appdef.header.edit", _appName),
    };

    /// <summary>New field name</summary>
    public string NewFieldName
    {
        get => _newFieldName;
        set => SetProperty(ref _newFieldName, value);
    }

    /// <summary>New field type (Core ValueType, defaults to system.string)</summary>
    public string NewFieldType
    {
        get => _newFieldType;
        set => SetProperty(ref _newFieldType, value);
    }

    /// <summary>Operation status message</summary>
    public string? Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    /// <summary>Whether a load/save/delete operation is running.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    /// <summary>App property node (complete property set driven by Attach); the view wraps it.</summary>
    public IValueAccess? AppNode
    {
        get => _appNode;
        private set => SetProperty(ref _appNode, value);
    }

    /// <summary>
    /// App Fields area: the whole field collection is one ArrayNode; it renders through the
    /// registered table template (<c>system.schema.app.field.schemas</c>), so no custom row
    /// model is kept here. The array node is rebuilt on add/remove (Core's DelRows does not
    /// dispose removed elements), while single-row edits go through the table cells directly.
    /// </summary>
    public ArrayNode? FieldsNode
    {
        get => _fieldsNode;
        private set => SetProperty(ref _fieldsNode, value);
    }

    /// <summary>Registry inherited by the app views and their nested schema views.</summary>
    public SchemaViewRegistry Registry => _registry;

    public ICommand LoadCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand AddFieldCommand { get; }
    public ICommand RemoveFieldCommand { get; }
    public ICommand BackCommand { get; }

    #region Navigation

    public bool IsNavigationTarget(NavigationContext navigationContext) => false;

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        Teardown();
        try
        {
            _session = _factory.CreateSession();

            string? mode = navigationContext.Parameters.GetValue<string>(ModeParameter);
            _mode = mode is ModeView or ModeEdit or ModeNew ? mode : ModeEdit;
            _container = navigationContext.Parameters.GetValue<string>(ContainerParameter) ?? string.Empty;
            string? app = navigationContext.Parameters.GetValue<string>(AppParameter);

            RaisePropertyChanged(nameof(Mode));
            RaisePropertyChanged(nameof(IsEditable));
            RaisePropertyChanged(nameof(HeaderText));

            if (_mode == ModeNew)
            {
                // Nav-driven creation: the container is fixed, the user completes the app name.
                AppName = _container.Length > 0 ? _container + "." : string.Empty;
                Status = LocaleManager.Instance.Get("wpf.appdef.status.newname");
                return;
            }

            if (string.IsNullOrWhiteSpace(app))
            {
                Status = LocaleManager.Instance.Get("wpf.appdef.status.ready");
                return;
            }

            AppName = app;
            _ = LoadFromNavigationAsync(app);
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    public void OnNavigatedFrom(NavigationContext navigationContext) => Teardown();

    public void Destroy() => Teardown();

    private void Teardown()
    {
        ClearNodes();
        _originalFieldNames.Clear();
        _session?.Dispose();
        _session = null;
        _mode = ModeEdit;
        _container = string.Empty;
        _isNavigating = false;
        Status = null;
        IsBusy = false;
    }

    private async Task LoadFromNavigationAsync(string fullName)
    {
        IsBusy = true;
        try
        {
            await LoadAppAsync(fullName);
        }
        catch (Exception ex)
        {
            Status = LocaleManager.Instance.Format("wpf.appdef.status.loadfailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Navigates back to the app list with the current container preserved. Awaits the Prism
    /// result so a missing route surfaces in the status instead of leaving a stale region.
    /// </summary>
    private async void ExecuteBack()
    {
        if (_isNavigating) return;
        _isNavigating = true;
        try
        {
            NavigationParameters parameters = new()
            {
                { AppListViewModel.NamespaceParameter, _container },
            };

            TaskCompletionSource<INavigationResult> completion = new();
            _regionManager.RequestNavigate(
                MainContentRegion,
                nameof(AppListView),
                result => completion.TrySetResult(result),
                parameters);

            INavigationResult outcome = await completion.Task;
            if (!outcome.Success)
            {
                Status = LocaleManager.Instance.Format(
                    "wpf.appdef.status.loadfailed",
                    outcome.Exception?.Message ?? nameof(AppListView));
            }
        }
        finally
        {
            _isNavigating = false;
        }
    }

    #endregion

    #region Load / new / save / delete

    private async void ExecuteLoad()
    {
        if (_session is null) return;
        IsBusy = true;
        try
        {
            await LoadAppAsync(AppName.Trim());
        }
        catch (Exception ex)
        {
            Status = LocaleManager.Instance.Format("wpf.appdef.status.loadfailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void ExecuteNew()
    {
        if (_session is null) return;
        string fullName = AppName.Trim();
        if (string.IsNullOrWhiteSpace(fullName))
        {
            Status = LocaleManager.Instance.Get("wpf.appdef.status.newname");
            return;
        }

        IsBusy = true;
        try
        {
            var exist = await _session.Context.GetAppTypeAsync(fullName);
            if (exist is not null)
            {
                Status = LocaleManager.Instance.Format("wpf.appdef.status.exist", fullName);
                return;
            }

            await LoadAppAsync(fullName, isNew: true);
            Status = LocaleManager.Instance.Format("wpf.appdef.status.created", fullName);
        }
        catch (Exception ex)
        {
            Status = LocaleManager.Instance.Format("wpf.appdef.status.newfailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Loads (or initializes) the app definition node and the field editing area.
    /// Note: the app node must be created from a JSON seed rather than an AppSchema
    /// instance — the CLR object branch of StructNode calls ClearValue on Attach fields
    /// without PropertyInfo, which drops properties such as Auths / Relations.
    /// </summary>
    private async Task LoadAppAsync(string fullName, bool isNew = false)
    {
        if (_session is null || string.IsNullOrWhiteSpace(fullName)) return;

        ClearNodes();
        _originalFieldNames.Clear();

        AppSchema? schema = isNew ? null : await _session.Api.GetAppSchemaAsync(fullName, includeTypes: true);
        if (!isNew && schema is null)
        {
            Status = LocaleManager.Instance.Format("wpf.appdef.status.notfound", fullName);
            return;
        }

        string container;
        string name;
        if (isNew)
        {
            int lastDot = fullName.LastIndexOf('.');
            container = lastDot < 0 ? string.Empty : fullName[..lastDot];
            name = lastDot < 0 ? fullName : fullName[(lastDot + 1)..];
        }
        else
        {
            container = schema!.Container ?? string.Empty;
            name = schema.Name;
        }

        _container = container;
        RaisePropertyChanged(nameof(HeaderText));

        StructType? appType = await _session.Context.GetNodeTypeAsync<StructType>($"{NS_SYSTEM_SCHEMA_APP}.schema");
        StructType? fieldType = await _session.Context.GetNodeTypeAsync<StructType>($"{NS_SYSTEM_SCHEMA_APP_FIELD}.schema");
        if (appType is null || fieldType is null)
        {
            Status = LocaleManager.Instance.Get("wpf.appdef.status.notype");
            return;
        }

        JsonObject appSeed = BuildAppSeed(schema, container, name);
        IValueAccess? appNode = appType.Create(_session.Context, appSeed);
        if (appNode is null)
        {
            Status = LocaleManager.Instance.Get("wpf.appdef.status.createfailed");
            return;
        }

        AppNode = appNode;
        if (appNode is IDisposable disposable)
            _session.TrackNode(disposable);

        // An existing app must not change its identity in this screen (Name is the primary key;
        // renaming is equivalent to creating another app).
        if (!isNew && appNode.GetAccessValue("name") is { } nameField)
            nameField.SetPropertyValue<ReadOnly, bool>(true, null, null);

        await RebuildFieldsNodeAsync(BuildFieldsSeed(schema?.Fields));

        if (!isNew)
        {
            _originalFieldNames.AddRange(schema!.Fields?.Select(f => f.Name) ?? []);
            AppName = schema.FullName;
            Status = LocaleManager.Instance.Format("wpf.appdef.status.loaded", schema.FullName, _originalFieldNames.Count);
        }
    }

    private async void ExecuteSave()
    {
        if (_session is null || _appNode is null) return;
        IsBusy = true;
        try
        {
            string? appName = _appNode.GetAccessValue("name")?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(appName))
            {
                Status = LocaleManager.Instance.Get("wpf.appdef.status.noname");
                return;
            }

            AppSchema? app = _appNode.GetValue<AppSchema>();
            if (app is null)
            {
                Status = LocaleManager.Instance.Get("wpf.appdef.status.readback");
                return;
            }

            AppFieldSchema[] fields = ReadFields();
            if (!ValidateFields(fields)) return;

            // Core's SaveAppSchemaAsync only saves fields listed in app.Fields and does not
            // delete removed fields, so delete "original field names - current field names"
            // here first.
            foreach (string removed in _originalFieldNames
                         .Where(old => !fields.Any(f => f.Name.Equals(old, StringComparison.OrdinalIgnoreCase))))
            {
                await _session.Context.DeleteAppFieldSchemaAsync(app.FullName, removed);
            }

            app.Fields = fields.Length > 0 ? fields : null;
            // Sub-apps / workflows / associated types are out of scope for this screen; set them
            // to null explicitly so they are not overwritten wholesale.
            // (AppSchema.Apps has an internal setter, so the read-back value is null anyway.)
            app.Workflows = null;
            app.NodeSchemas = null;

            bool ok = await _session.Api.SaveAppSchemaAsync(app);
            if (!ok)
            {
                Status = LocaleManager.Instance.Format("wpf.appdef.status.savefailed", app.FullName);
                return;
            }

            // Saving invalidates AppType; rebuild it using the existing reload: true convention.
            await _session.Context.GetAppTypeAsync(app.FullName, reload: true);
            AppName = app.FullName;
            _mode = ModeEdit;
            RaisePropertyChanged(nameof(Mode));
            RaisePropertyChanged(nameof(IsEditable));
            RaisePropertyChanged(nameof(HeaderText));
            await LoadAppAsync(app.FullName);
            Status = LocaleManager.Instance.Format("wpf.appdef.status.saved", app.FullName);
        }
        catch (Exception ex)
        {
            Status = LocaleManager.Instance.Format("wpf.appdef.status.saveerror", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void ExecuteDelete()
    {
        if (_session is null) return;
        string fullName = AppName.Trim();
        if (string.IsNullOrWhiteSpace(fullName)) return;

        IsBusy = true;
        try
        {
            var node = await _session.Context.GetAppTypeAsync(fullName);
            if (node is null)
            {
                Status = LocaleManager.Instance.Format("wpf.appdef.status.missing", fullName);
                return;
            }

            // Core constraint: AppType.IsUsed = has fields or has sub-apps; DeleteAppSchemaAsync
            // then returns false directly.
            if (node.IsUsed)
            {
                Status = LocaleManager.Instance.Get("wpf.appdef.status.inuse");
                return;
            }

            bool ok = await _session.Api.DeleteAppSchemaAsync(fullName);
            if (!ok)
            {
                Status = LocaleManager.Instance.Format("wpf.appdef.status.deletefailed", fullName);
                return;
            }

            await _session.Context.GetAppTypeAsync(fullName, reload: true);
            ClearNodes();
            _originalFieldNames.Clear();
            AppName = string.Empty;
            Status = LocaleManager.Instance.Format("wpf.appdef.status.deleted", fullName);
        }
        catch (Exception ex)
        {
            Status = LocaleManager.Instance.Format("wpf.appdef.status.deleteerror", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion

    #region App Fields

    private async void ExecuteAddField()
    {
        if (_session is null || _fieldsNode is null) return;

        string name = NewFieldName.Trim();
        string type = NewFieldType.Trim();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(type))
        {
            Status = LocaleManager.Instance.Get("wpf.appdef.field.status.empty");
            return;
        }

        JsonArray current = GetFieldsJson();
        if (current.Any(item =>
                item is JsonObject obj &&
                obj["name"] is JsonValue value &&
                value.TryGetValue<string>(out string? existName) &&
                string.Equals(existName, name, StringComparison.OrdinalIgnoreCase)))
        {
            Status = LocaleManager.Instance.Format("wpf.appdef.field.status.exist", name);
            return;
        }

        IsBusy = true;
        try
        {
            JsonObject item = new()
            {
                ["name"] = name,
                ["type"] = type,
            };
            current.Add(item);

            await RebuildFieldsNodeAsync(current);
            NewFieldName = string.Empty;
            Status = LocaleManager.Instance.Format("wpf.appdef.field.status.added", name);
        }
        catch (Exception ex)
        {
            Status = LocaleManager.Instance.Format("wpf.appdef.field.status.addfailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void ExecuteRemoveField(DataNodeWrapper? cell)
    {
        if (_session is null || _fieldsNode is null || cell is null) return;
        if (!TryGetArrayIndex(cell, out int index)) return;

        IsBusy = true;
        try
        {
            JsonArray current = GetFieldsJson();
            if (index < 0 || index >= current.Count) return;

            string? name = current[index] is JsonObject obj
                ? obj["name"]?.GetValue<string>()
                : null;
            current.RemoveAt(index);
            await RebuildFieldsNodeAsync(current);
            Status = LocaleManager.Instance.Format("wpf.appdef.field.status.removed", name ?? $"[{index}]");
        }
        catch (Exception ex)
        {
            Status = LocaleManager.Instance.Format("wpf.appdef.field.status.removefailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Resolves the element index of a table cell: cell node → parent struct element → parent
    /// array. The same chain is used by <see cref="ArrayWrapper"/>.
    /// </summary>
    private static bool TryGetArrayIndex(DataNodeWrapper cell, out int index)
    {
        index = -1;
        if (cell.Node is not DataNode fieldNode || fieldNode.Parent is not DataNode element) return false;
        if (element.Parent is not ArrayNode array) return false;
        index = array.IndexOf(element);
        return index >= 0;
    }

    /// <summary>
    /// Rebuilds the field array node from the current field JSON. Adding and removing fields
    /// rebuilds the whole table, avoiding subscription leaks caused by ArrayNode.DelRows not
    /// disposing removed elements; single-row edits still go through their own Wrapper and do
    /// not trigger a rebuild.
    /// </summary>
    private async Task RebuildFieldsNodeAsync(JsonArray seed)
    {
        if (_session is null) return;

        ClearFieldNodes();

        StructType? fieldType = await _session.Context.GetNodeTypeAsync<StructType>($"{NS_SYSTEM_SCHEMA_APP_FIELD}.schema");
        if (fieldType is null) return;

        ArrayType? arrayType = await _session.Context.GetArrayNodeTypeAsync(fieldType);
        if (arrayType?.Create(_session.Context, seed.DeepClone()) is not ArrayNode array)
            return;

        FieldsNode = array;
        if (array is IDisposable disposable)
            _session.TrackNode(disposable);
    }

    private AppFieldSchema[] ReadFields()
    {
        if (_fieldsNode?.TryGetValue<AppFieldSchema[]>(out AppFieldSchema[]? fields) == true && fields is not null)
            return fields;
        return [];
    }

    private bool ValidateFields(AppFieldSchema[] fields)
    {
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        foreach (AppFieldSchema field in fields)
        {
            if (string.IsNullOrWhiteSpace(field.Name))
            {
                Status = LocaleManager.Instance.Get("wpf.appdef.field.status.noname");
                return false;
            }
            if (string.IsNullOrWhiteSpace(field.Type))
            {
                Status = LocaleManager.Instance.Format("wpf.appdef.field.status.notype", field.Name);
                return false;
            }
            if (!names.Add(field.Name))
            {
                Status = LocaleManager.Instance.Format("wpf.appdef.field.status.duplicate", field.Name);
                return false;
            }
        }
        return true;
    }

    private JsonArray GetFieldsJson()
        => _fieldsNode?.GetValue<JsonArray>() ?? new JsonArray();

    private void ClearFieldNodes()
    {
        // Clearing the binding lets the view dispose the table wrapper first; the array node itself
        // is released afterwards (disposing the array cascades to its elements).
        ArrayNode? node = _fieldsNode;
        FieldsNode = null;

        if (node is IDisposable disposable)
        {
            if (_session is null)
                disposable.Dispose();
            else
                _session.ReleaseNode(disposable);
        }
    }

    #endregion

    #region Seeds / teardown

    private static JsonObject BuildAppSeed(AppSchema? schema, string container, string name)
    {
        JsonObject seed = schema?.Extensions?.DeepClone() as JsonObject ?? new JsonObject();
        if (!string.IsNullOrWhiteSpace(container))
            seed["container"] = container;
        if (!string.IsNullOrWhiteSpace(name))
            seed["name"] = name;
        return seed;
    }

    private static JsonArray BuildFieldsSeed(AppFieldSchema[]? fields)
    {
        JsonArray array = [];
        foreach (AppFieldSchema field in fields ?? [])
        {
            JsonObject item = field.Extensions?.DeepClone() as JsonObject ?? new JsonObject();
            item["name"] = field.Name;
            item["type"] = field.Type;
            array.Add(item);
        }
        return array;
    }

    private void ClearNodes()
    {
        ClearFieldNodes();

        IValueAccess? node = _appNode;
        AppNode = null;

        if (node is IDisposable disposable)
        {
            if (_session is null)
                disposable.Dispose();
            else
                _session.ReleaseNode(disposable);
        }
    }

    #endregion
}
