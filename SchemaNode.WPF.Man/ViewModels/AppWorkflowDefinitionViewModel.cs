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
/// Workflow reference screen (view / edit / new) for one app workflow.
///
/// Like <see cref="AppFieldDefinitionViewModel"/>, the app workflow no longer owns the workflow
/// graph: it only references an existing workflow type through the <c>type</c> field and keeps
/// its own active flag. The editor is therefore a SchemaView over the Attach-driven
/// <c>system.schema.app.workflow.schema</c> type, so the property groups come from Attach metadata.
/// </summary>
public class AppWorkflowDefinitionViewModel : BindableBase, INavigationAware, IDestructible
{
    public const string AppParameter = "app";
    public const string WorkflowParameter = "workflow";
    public const string ModeParameter = "mode";

    public const string ModeView = "view";
    public const string ModeEdit = "edit";
    public const string ModeNew = "new";

    private const string MainContentRegion = "MainContentRegion";
    private const string WorkflowSchemaTypeName = $"{NS_SYSTEM_SCHEMA_APP_WORKFLOW}.schema";

    private readonly SchemaContextFactory _factory;
    private readonly SchemaViewRegistry _registry;
    private readonly IRegionManager _regionManager;

    private SchemaScreenSession? _session;
    private string _app = string.Empty;
    private string _workflowName = string.Empty;
    private string _mode = ModeEdit;
    private string? _status;
    private bool _isBusy;
    private bool _isNavigating;
    private IValueAccess? _workflowNode;

    public AppWorkflowDefinitionViewModel(
        SchemaContextFactory factory,
        SchemaViewRegistry registry,
        IRegionManager regionManager)
    {
        _factory = factory;
        _registry = registry;
        _regionManager = regionManager;

        SaveCommand = new DelegateCommand(ExecuteSave, () => WorkflowNode is not null && IsEditable && !IsBusy)
            .ObservesProperty(() => WorkflowNode)
            .ObservesProperty(() => IsBusy)
            .ObservesProperty(() => IsEditable);
        BackCommand = new DelegateCommand(ExecuteBack);
    }

    #region Bindable state

    /// <summary>Full name of the owning app.</summary>
    public string AppName => _app;

    /// <summary>Workflow reference name (empty while creating a new one).</summary>
    public string WorkflowName => _workflowName;

    /// <summary>Current document mode (view / edit / new).</summary>
    public string Mode => _mode;

    /// <summary>Whether the editor accepts input.</summary>
    public bool IsEditable => _mode is ModeEdit or ModeNew;

    /// <summary>Localized header describing the mode and subject.</summary>
    public string HeaderText => _mode switch
    {
        ModeNew => LocaleManager.Instance.Format("wpf.appworkflowdef.header.new", _app),
        ModeView => LocaleManager.Instance.Format("wpf.appworkflowdef.header.view", _workflowName),
        _ => LocaleManager.Instance.Format("wpf.appworkflowdef.header.edit", _workflowName),
    };

    /// <summary>SchemaView node carrying the Attach-driven workflow reference properties.</summary>
    public DataNodeWrapper? WorkflowNode { get; private set; }

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
            _workflowName = navigationContext.Parameters.GetValue<string>(WorkflowParameter) ?? string.Empty;
            string? mode = navigationContext.Parameters.GetValue<string>(ModeParameter);
            _mode = mode is ModeView or ModeEdit or ModeNew ? mode : ModeEdit;

            RaisePropertyChanged(nameof(AppName));
            RaisePropertyChanged(nameof(WorkflowName));
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
            Status = LocaleManager.Instance.Get("wpf.appworkflowdef.status.noname");
            return;
        }

        IsBusy = true;
        Status = _mode == ModeNew
            ? LocaleManager.Instance.Get("wpf.appworkflowdef.status.newready")
            : LocaleManager.Instance.Format("wpf.appworkflowdef.status.loading", _workflowName);
        try
        {
            AppWorkflowSchema? schema = null;
            if (_mode != ModeNew)
            {
                AppSchema? appSchema = await _session.Api.GetAppSchemaAsync(_app, includeTypes: true);
                schema = appSchema?.Workflows?.FirstOrDefault(
                    workflow => workflow.Name.Equals(_workflowName, StringComparison.Ordinal));
                if (schema is null)
                {
                    Status = LocaleManager.Instance.Format("wpf.appworkflowdef.status.notfound", _workflowName);
                    return;
                }
            }

            StructType? type = await _session.Context.GetNodeTypeAsync<StructType>(WorkflowSchemaTypeName);
            if (type is null)
            {
                Status = LocaleManager.Instance.Get("wpf.appworkflowdef.status.notype");
                return;
            }

            JsonObject seed = BuildSeed(schema, _app, _workflowName);
            IValueAccess? node = type.Create(_session.Context, seed);
            if (node is null)
            {
                Status = LocaleManager.Instance.Format("wpf.appworkflowdef.status.error", WorkflowSchemaTypeName);
                return;
            }

            WorkflowNode = NodeWrapperFactory.Create(node, _registry);
            _workflowNode = node;
            if (node is IDisposable disposable)
                _session.TrackNode(disposable);

            // Name is the primary index of the app workflow reference; it cannot be renamed in place.
            if (_mode != ModeNew && node.GetAccessValue("name") is { } nameField)
                nameField.SetPropertyValue<ReadOnly, bool>(true, null, null);

            if (_mode != ModeNew)
            {
                _workflowName = schema!.Name;
                RaisePropertyChanged(nameof(WorkflowName));
                RaisePropertyChanged(nameof(HeaderText));
                Status = LocaleManager.Instance.Format("wpf.appworkflowdef.status.loaded", _workflowName);
            }
        }
        catch (Exception ex)
        {
            Status = LocaleManager.Instance.Format("wpf.appworkflowdef.status.error", ex.Message);
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
        if (session is null || _workflowNode is null || !IsEditable) return;

        IsBusy = true;
        try
        {
            IValueAccess node = _workflowNode;
            string? name = node.GetAccessValue("name")?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(name))
            {
                Status = LocaleManager.Instance.Get("wpf.appworkflowdef.status.noname");
                return;
            }

            string? workflowType = node.GetAccessValue("type")?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(workflowType))
            {
                Status = LocaleManager.Instance.Get("wpf.appworkflowdef.status.typeempty");
                return;
            }

            AppWorkflowSchema? workflow = node.GetValue<AppWorkflowSchema>();
            if (workflow is null)
            {
                Status = LocaleManager.Instance.Format("wpf.appworkflowdef.status.error", name);
                return;
            }

            workflow.App = _app;
            workflow.Name = name.Trim();
            workflow.Type = workflowType;

            bool saved = await Task.Run(() => session.Context.SaveAppWorkflowSchemaAsync(_app, workflow));
            if (!saved)
            {
                Status = LocaleManager.Instance.Format("wpf.appworkflowdef.status.savefailed", workflow.Name);
                return;
            }

            // Saved reference is now an existing one: switch to edit mode and lock the name.
            _workflowName = workflow.Name;
            _mode = ModeEdit;
            RaisePropertyChanged(nameof(WorkflowName));
            RaisePropertyChanged(nameof(Mode));
            RaisePropertyChanged(nameof(IsEditable));
            RaisePropertyChanged(nameof(HeaderText));
            node.GetAccessValue("name")?.SetPropertyValue<ReadOnly, bool>(true, null, null);
            Status = LocaleManager.Instance.Format("wpf.appworkflowdef.status.saved", workflow.Name);
        }
        catch (Exception ex)
        {
            Status = LocaleManager.Instance.Format("wpf.appworkflowdef.status.savefailed", ex.Message);
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
                nameof(AppWorkflowListView),
                result => completion.TrySetResult(result),
                new NavigationParameters
                {
                    { AppWorkflowListViewModel.AppParameter, _app },
                });

            INavigationResult outcome = await completion.Task;
            if (!outcome.Success)
            {
                Status = LocaleManager.Instance.Format(
                    "wpf.appworkflowdef.status.navfailed",
                    outcome.Exception?.Message ?? nameof(AppWorkflowListView));
            }
        }
        finally
        {
            _isNavigating = false;
        }
    }

    #endregion

    private static JsonObject BuildSeed(AppWorkflowSchema? schema, string app, string name)
    {
        JsonObject seed = schema?.Extensions?.DeepClone() as JsonObject ?? new JsonObject();
        seed["app"] = app;
        if (!string.IsNullOrWhiteSpace(name))
            seed["name"] = name;
        if (!string.IsNullOrWhiteSpace(schema?.Type))
            seed["type"] = schema!.Type;
        if (schema is not null)
            seed["active"] = schema.Active;
        return seed;
    }

    private void Teardown()
    {
        DataNodeWrapper? wrapper = WorkflowNode;
        WorkflowNode = null;
        wrapper?.Dispose();

        IValueAccess? node = _workflowNode;
        _workflowNode = null;
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
        _workflowName = string.Empty;
        _mode = ModeEdit;
        _isNavigating = false;
        Status = null;
        IsBusy = false;
    }
}
