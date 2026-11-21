using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using Prism.Navigation.Regions;
using SchemaNode.Api.Client;
using SchemaNode.Node;
using SchemaNode.Property.Common;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.WPF.Man.Services;
using SchemaNode.WPF.Man.Views;
using SchemaNode.WPF.View.Localization;
using static SchemaNode.Utility.AppConstant;
using StructType = SchemaNode.Runtime.StructType;

namespace SchemaNode.WPF.Man.ViewModels;

/// <summary>
/// Workflow definition editor (screen scope).
///
/// The workflow graph belongs to a workflow type, not to an app: the definition is stored on
/// the schema node as the <c>workflow</c> property. The data model is:
///   - the root node is <c>system.schema.workflow.schema</c> (WorkflowSchema);
///   - <c>Steps[]</c> is an array of <c>system.schema.workflow.step</c> (WorkflowStepSchema);
///   - incoming edges are expressed by the step's <c>Previous</c> (array of predecessor names); outgoing edges are the Previous entries of other steps pointing to this step;
///   - saving writes the definition back to the node schema through <see cref="ISchemaApiProvider.SaveSchemaAsync"/>.
///
/// The screen lifecycle matches other screens: navigation activation creates an independent SchemaContext; on leave,
/// the Wrapper (selected property panel) → DataNode → Context/Scope are destroyed in reverse order.
///
/// Navigation contract: <c>node</c> (required, the workflow type full name), <c>namespace</c> (owner namespace,
/// used by the back navigation) and <c>mode</c> (view / edit).
/// </summary>
public class WorkflowConfigViewModel : BindableBase, INavigationAware, IDestructible
{
    public const string DefinitionParameter = "node";
    public const string NamespaceParameter = "namespace";
    public const string ModeParameter = "mode";

    public const string ModeView = "view";
    public const string ModeEdit = "edit";

    private const string MainContentRegion = "MainContentRegion";
    private const string DefinitionTypeName = $"{NS_SYSTEM_SCHEMA_WORKFLOW}.schema";

    private readonly SchemaContextFactory _factory;
    private readonly IRegionManager _regionManager;
    private SchemaScreenSession? _session;

    private string _definitionName = string.Empty;
    private string _namespace = string.Empty;
    private string _status = string.Empty;
    private double _zoom = 1.0;
    private double _canvasWidth = 600;
    private double _canvasHeight = 400;
    private string _mode = ModeEdit;
    private bool _isNavigating;
    private bool _isSaving;

    private NodeSchema? _definitionSchema;

    private IValueAccess? _workflowNode;
    private ArrayNode? _nodesArray;
    private IDisposable? _nodesSubscription;
    private CancellationTokenSource? _reloadCts;
    private int _loadVersion;

    private readonly Dictionary<IValueAccess, (double X, double Y)> _positions =
        new(ReferenceEqualityComparer.Instance);

    private WorkflowNodeViewModel? _selectedNode;
    private WorkflowNodeViewModel? _pendingSource;
    private WorkflowEdgeViewModel? _selectedEdge;
    private bool _isConnecting;

    public WorkflowConfigViewModel(SchemaContextFactory factory, IRegionManager regionManager)
    {
        _factory = factory;
        _regionManager = regionManager;

        SaveCommand = new DelegateCommand(ExecuteSave, () => !_isSaving)
            .ObservesProperty(() => IsSaving);
        AddNodeCommand = new DelegateCommand(ExecuteAddNode);
        DeleteNodeCommand = new DelegateCommand(ExecuteDeleteNode);
        DisconnectEdgeCommand = new DelegateCommand(ExecuteDisconnectEdge);
        BackCommand = new DelegateCommand(ExecuteBack);
        ZoomInCommand = new DelegateCommand(() => Zoom = Math.Min(2.0, Zoom + 0.1));
        ZoomOutCommand = new DelegateCommand(() => Zoom = Math.Max(0.4, Zoom - 0.1));
        ResetZoomCommand = new DelegateCommand(() => Zoom = 1.0);
    }

    #region Properties

    /// <summary>Full name of the workflow type whose definition is being edited.</summary>
    public string DefinitionName => _definitionName;

    /// <summary>Localized header describing the mode and subject.</summary>
    public string HeaderText => _mode == ModeView
        ? LocaleManager.Instance.Format("wpf.workflow.header.definition.view", _definitionName)
        : LocaleManager.Instance.Format("wpf.workflow.header.definition", _definitionName);

    /// <summary>Current document mode (view / edit).</summary>
    public string Mode => _mode;

    /// <summary>Whether the canvas accepts mutations; false in view mode.</summary>
    public bool IsEditable => _mode == ModeEdit;

    /// <summary>Whether a save is in flight; the canvas is frozen meanwhile.</summary>
    public bool IsSaving => _isSaving;

    /// <summary>Whether the save command is available.</summary>
    public bool CanSave => IsEditable && !_isSaving;

    /// <summary>
    /// Whether the canvas has no steps. The empty state replaces the canvas with a single button
    /// adding the first node; the canvas never inserts a row on its own.
    /// </summary>
    public bool ShowEmptyState => IsEditable && Nodes.Count == 0;

    /// <summary>Workflow definition name. The latest schema no longer stores a name on the workflow payload, so the type name is used.</summary>
    public string WorkflowName => _definitionName;

    /// <summary>Canvas node collection</summary>
    public ObservableCollection<WorkflowNodeViewModel> Nodes { get; } = new();

    /// <summary>Canvas edge collection</summary>
    public ObservableCollection<WorkflowEdgeViewModel> Edges { get; } = new();

    /// <summary>Currently selected node (creates the property panel Wrapper on demand when selected)</summary>
    public WorkflowNodeViewModel? SelectedNode
    {
        get => _selectedNode;
        private set
        {
            if (ReferenceEquals(_selectedNode, value)) return;

            if (_selectedNode is not null)
                _selectedNode.IsSelected = false;
            if (value is not null)
                value.IsSelected = true;

            SetProperty(ref _selectedNode, value);
        }
    }

    /// <summary>Currently selected edge</summary>
    public WorkflowEdgeViewModel? SelectedEdge
    {
        get => _selectedEdge;
        private set
        {
            if (ReferenceEquals(_selectedEdge, value)) return;

            if (_selectedEdge is not null)
                _selectedEdge.IsSelected = false;
            if (value is not null)
                value.IsSelected = true;

            SetProperty(ref _selectedEdge, value);
        }
    }

    /// <summary>Connect mode: click the source first, then the target</summary>
    public bool IsConnecting
    {
        get => _isConnecting;
        set
        {
            if (!IsEditable) return;
            if (!SetProperty(ref _isConnecting, value)) return;
            _pendingSource = null;
            Status = value
                ? LocaleManager.Instance.Get("wpf.workflow.status.connect.on")
                : LocaleManager.Instance.Get("wpf.workflow.status.connect.off");
        }
    }

    /// <summary>Name of the pending source node (for status display)</summary>
    public string PendingSourceName => _pendingSource?.Name ?? string.Empty;

    /// <summary>Canvas zoom</summary>
    public double Zoom
    {
        get => _zoom;
        set => SetProperty(ref _zoom, value);
    }

    public double CanvasWidth
    {
        get => _canvasWidth;
        private set => SetProperty(ref _canvasWidth, value);
    }

    public double CanvasHeight
    {
        get => _canvasHeight;
        private set => SetProperty(ref _canvasHeight, value);
    }

    /// <summary>Status message</summary>
    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public ICommand SaveCommand { get; }
    public ICommand AddNodeCommand { get; }
    public ICommand DeleteNodeCommand { get; }
    public ICommand DisconnectEdgeCommand { get; }
    public ICommand ZoomInCommand { get; }
    public ICommand ZoomOutCommand { get; }
    public ICommand ResetZoomCommand { get; }
    public ICommand BackCommand { get; }

    #endregion

    #region Navigation

    public bool IsNavigationTarget(NavigationContext navigationContext) => false;

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        Teardown();
        _session = _factory.CreateSession();

        string? mode = navigationContext.Parameters.GetValue<string>(ModeParameter);
        _mode = mode == ModeView ? ModeView : ModeEdit;
        _definitionName = navigationContext.Parameters.GetValue<string>(DefinitionParameter) ?? string.Empty;
        _namespace = navigationContext.Parameters.GetValue<string>(NamespaceParameter) ?? string.Empty;

        RaisePropertyChanged(nameof(Mode));
        RaisePropertyChanged(nameof(IsEditable));
        RaisePropertyChanged(nameof(CanSave));
        RaisePropertyChanged(nameof(ShowEmptyState));
        RaisePropertyChanged(nameof(DefinitionName));
        RaisePropertyChanged(nameof(HeaderText));

        Status = LocaleManager.Instance.Get("wpf.workflow.status.definition.ready");

        if (!string.IsNullOrWhiteSpace(_definitionName))
            _ = InitializeAsync();
    }

    public void OnNavigatedFrom(NavigationContext navigationContext) => Teardown();

    public void Destroy() => Teardown();

    private void Teardown()
    {
        TeardownWorkflow();

        _session?.Dispose();
        _session = null;
        _mode = ModeEdit;
        _definitionName = string.Empty;
        _namespace = string.Empty;
        _isNavigating = false;
        _definitionSchema = null;
        Status = string.Empty;
        RaisePropertyChanged(nameof(DefinitionName));
        RaisePropertyChanged(nameof(HeaderText));
    }

    /// <summary>
    /// Loads the workflow type schema and its <c>workflow</c> into the canvas. Runs after the
    /// view is bound so status updates are visible.
    /// </summary>
    private async Task InitializeAsync()
    {
        if (_session is null) return;

        string name = _definitionName;
        try
        {
            NodeSchema[] schemas = await _session.Api.GetSchemaAsync([name]);
            NodeSchema? definition = schemas.FirstOrDefault(
                schema => schema.FullName.Equals(name, StringComparison.OrdinalIgnoreCase))
                ?? schemas.FirstOrDefault();
            if (definition is null)
            {
                Status = LocaleManager.Instance.Format("wpf.workflow.status.definition.notfound", name);
                return;
            }

            _definitionSchema = definition;
            WorkflowSchema? source = definition.GetProperty<WorkflowProperty>()?.Value;
            WorkflowSchema workflow = source is null
                ? new WorkflowSchema { Steps = [] }
                : Clone(source);

            StructType? type = await _session.Context.GetNodeTypeAsync<StructType>(DefinitionTypeName);
            if (type?.Create(_session.Context, workflow) is not { } node)
            {
                Status = LocaleManager.Instance.Get("wpf.workflow.status.typenotready");
                return;
            }

            _workflowNode = node;
            if (node is IDisposable disposable)
                _session.TrackNode(disposable);

            _nodesArray = node.GetAccessValue(nameof(WorkflowSchema.Steps)) as ArrayNode;

            if (_nodesArray is null)
            {
                Status = LocaleManager.Instance.Get("wpf.workflow.status.nonodes");
            }
            else
            {
                _nodesSubscription = _nodesArray.Subscribe(_ => ScheduleGraphReload());
            }

            RaisePropertyChanged(nameof(WorkflowName));
            RebuildGraph(forceStructure: true);
            Status = LocaleManager.Instance.Format("wpf.workflow.status.loaded", definition.FullName);
        }
        catch (Exception ex)
        {
            Status = LocaleManager.Instance.Format("wpf.workflow.status.loadfailed", ex.Message);
        }
    }

    /// <summary>Navigates back to the type definition list with the namespace preserved.</summary>
    private async void ExecuteBack()
    {
        if (_isNavigating) return;
        _isNavigating = true;
        try
        {
            TaskCompletionSource<INavigationResult> completion = new();
            _regionManager.RequestNavigate(
                MainContentRegion,
                nameof(TypeListView),
                result => completion.TrySetResult(result),
                new NavigationParameters
                {
                    { TypeDocumentViewModel.NamespaceParameter, _namespace },
                });

            INavigationResult outcome = await completion.Task;
            if (!outcome.Success)
            {
                Status = LocaleManager.Instance.Format(
                    "wpf.workflow.status.backfailed",
                    outcome.Exception?.Message ?? nameof(TypeListView));
            }
        }
        finally
        {
            _isNavigating = false;
        }
    }

    #endregion

    #region Load / Save

    private void SetSaving(bool saving)
    {
        if (_isSaving == saving) return;
        _isSaving = saving;
        RaisePropertyChanged(nameof(IsSaving));
        RaisePropertyChanged(nameof(CanSave));
    }

    private async void ExecuteSave()
    {
        if (!IsEditable)
        {
            Status = LocaleManager.Instance.Get("wpf.workflow.status.readonly");
            return;
        }
        if (_session is null || _workflowNode is null || _definitionSchema is null)
        {
            Status = LocaleManager.Instance.Get("wpf.workflow.status.loadfirst");
            return;
        }

        // The property panel may have just changed a name; align the canvas projection with the Core fields before validating
        foreach (WorkflowNodeViewModel node in Nodes)
            node.Refresh();

        string? error = ValidateWorkflow();
        if (error is not null)
        {
            Status = error;
            return;
        }

        WorkflowSchema? definition = _workflowNode.GetValue<WorkflowSchema>();
        if (definition is null)
        {
            Status = LocaleManager.Instance.Get("wpf.workflow.status.nodata");
            return;
        }

        // The definition is named after its workflow type.
        _definitionSchema.SetProperty<WorkflowProperty, WorkflowSchema>(definition);

        SchemaScreenSession session = _session;
        NodeSchema schema = _definitionSchema;
        SetSaving(true);
        try
        {
            bool ok = await Task.Run(() => session.Api.SaveSchemaAsync(schema));
            if (!ok)
            {
                Status = LocaleManager.Instance.Get("wpf.workflow.status.savefailed.active");
                return;
            }

            await RefreshOwnerNamespaceAsync(session, schema);
            Status = LocaleManager.Instance.Get("wpf.workflow.status.saved");
        }
        catch (Exception ex)
        {
            Status = LocaleManager.Instance.Format("wpf.workflow.status.savefailed", ex.Message);
        }
        finally
        {
            SetSaving(false);
        }
    }

    /// <summary>
    /// Reloads the namespace that owns the saved definition so the runtime picks the new definition up.
    /// A failure only reports the refresh error; the save itself already succeeded.
    /// </summary>
    private async Task RefreshOwnerNamespaceAsync(SchemaScreenSession session, NodeSchema schema)
    {
        if (string.IsNullOrWhiteSpace(schema.Namespace)) return;

        try
        {
            await session.Context.GetNodeTypeAsync(schema.Namespace, reload: true);
        }
        catch (Exception ex)
        {
            Status = LocaleManager.Instance.Format("wpf.workflow.status.loadfailed", ex.Message);
        }
    }

    private void TeardownWorkflow()
    {
        _loadVersion++;

        _reloadCts?.Cancel();
        _reloadCts?.Dispose();
        _reloadCts = null;

        _nodesSubscription?.Dispose();
        _nodesSubscription = null;

        ClearGraph();

        _nodesArray = null;

        if (_workflowNode is IDisposable disposable)
        {
            if (_session is null)
                disposable.Dispose();
            else
                _session.ReleaseNode(disposable);
        }
        _workflowNode = null;

        RaisePropertyChanged(nameof(WorkflowName));
    }

    #endregion

    #region Graph rebuild

    private void ScheduleGraphReload()
    {
        _reloadCts?.Cancel();
        _reloadCts?.Dispose();

        var cts = new CancellationTokenSource();
        _reloadCts = cts;
        _ = ReloadGraphAfterDelayAsync(cts);
    }

    private async Task ReloadGraphAfterDelayAsync(CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(200, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (cts.IsCancellationRequested) return;

        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            await dispatcher.InvokeAsync(() =>
            {
                if (!cts.IsCancellationRequested)
                    RebuildGraph(forceStructure: false);
            });
        }
        else
        {
            RebuildGraph(forceStructure: false);
        }
    }

    private void RebuildGraph(bool forceStructure)
    {
        if (_nodesArray is null)
        {
            ClearGraph();
            return;
        }

        bool structureChanged = forceStructure || IsStructureChanged();
        if (structureChanged)
        {
            IValueAccess? selectedRef = SelectedNode?.Node;

            foreach (WorkflowNodeViewModel node in Nodes)
                _positions[node.Node] = (node.X, node.Y);

            SelectedNode = null;
            SelectedEdge = null;

            foreach (WorkflowNodeViewModel node in Nodes)
                node.Dispose();
            Nodes.Clear();
            RemoveStalePositions();

            int index = 0;
            foreach (IValueAccess item in _nodesArray)
            {
                var node = new WorkflowNodeViewModel(item);
                if (_positions.TryGetValue(item, out (double X, double Y) position))
                {
                    node.X = position.X;
                    node.Y = position.Y;
                }
                else
                {
                    node.X = 40 + (index % 4) * 220;
                    node.Y = 40 + (index / 4) * 120;
                }

                Nodes.Add(node);
                index++;
            }

            if (selectedRef is not null)
                SelectedNode = Nodes.FirstOrDefault(n => ReferenceEquals(n.Node, selectedRef));
        }
        else
        {
            foreach (WorkflowNodeViewModel node in Nodes)
                node.Refresh();
        }

        RebuildEdges();
        UpdateCanvasSize();
        RaisePropertyChanged(nameof(ShowEmptyState));
    }

    private bool IsStructureChanged()
    {
        if (_nodesArray is null) return Nodes.Count != 0;
        if (Nodes.Count != _nodesArray.Count) return true;

        int index = 0;
        foreach (IValueAccess item in _nodesArray)
        {
            if (!ReferenceEquals(Nodes[index].Node, item)) return true;
            index++;
        }
        return false;
    }

    private void RemoveStalePositions()
    {
        if (_nodesArray is null) return;

        var alive = new HashSet<IValueAccess>(_nodesArray, ReferenceEqualityComparer.Instance);
        foreach (IValueAccess key in _positions.Keys.ToList())
            if (!alive.Contains(key))
                _positions.Remove(key);
    }

    private void RebuildEdges()
    {
        SelectedEdge = null;
        foreach (WorkflowEdgeViewModel edge in Edges)
            edge.Dispose();
        Edges.Clear();

        Dictionary<string, WorkflowNodeViewModel> byName = new(StringComparer.OrdinalIgnoreCase);
        foreach (WorkflowNodeViewModel node in Nodes)
        {
            if (!string.IsNullOrWhiteSpace(node.Name) && !byName.ContainsKey(node.Name))
                byName[node.Name] = node;
        }

        foreach (WorkflowNodeViewModel target in Nodes)
        {
            foreach (string previous in GetPrevious(target.Node))
            {
                if (!byName.TryGetValue(previous, out WorkflowNodeViewModel? source)) continue;
                if (ReferenceEquals(source, target)) continue;
                Edges.Add(new WorkflowEdgeViewModel(source, target));
            }
        }
    }

    private void UpdateCanvasSize()
    {
        double width = 600;
        double height = 400;

        foreach (WorkflowNodeViewModel node in Nodes)
        {
            width = Math.Max(width, node.X + WorkflowNodeViewModel.NodeWidth + 80);
            height = Math.Max(height, node.Y + WorkflowNodeViewModel.NodeHeight + 80);
        }

        CanvasWidth = width;
        CanvasHeight = height;
    }

    private void ClearGraph()
    {
        SelectedNode = null;
        SelectedEdge = null;

        foreach (WorkflowEdgeViewModel edge in Edges)
            edge.Dispose();
        Edges.Clear();

        foreach (WorkflowNodeViewModel node in Nodes)
            node.Dispose();
        Nodes.Clear();

        _positions.Clear();
        CanvasWidth = 600;
        CanvasHeight = 400;
        RaisePropertyChanged(nameof(ShowEmptyState));
    }

    #endregion

    #region Graph editing

    /// <summary>
    /// Node click: in normal mode selects the node; in connect mode collects the source/target.
    /// </summary>
    public void HandleNodeClick(WorkflowNodeViewModel node)
    {
        if (node is null) return;

        if (IsConnecting && IsEditable)
        {
            if (_pendingSource is null)
            {
                _pendingSource = node;
                SelectedNode = node;
                SelectedEdge = null;
                Status = LocaleManager.Instance.Format("wpf.workflow.status.connect.source", node.Name);
            }
            else if (ReferenceEquals(_pendingSource, node))
            {
                _pendingSource = null;
                Status = LocaleManager.Instance.Get("wpf.workflow.status.connect.cancelled");
            }
            else
            {
                WorkflowNodeViewModel source = _pendingSource;
                _pendingSource = null;
                TryConnect(source, node);
            }

            RaisePropertyChanged(nameof(PendingSourceName));
            return;
        }

        SelectedNode = node;
        SelectedEdge = null;
        Status = node.Name;
    }

    /// <summary>
    /// Edge click: selects that edge for use by "disconnect selected edge".
    /// </summary>
    public void HandleEdgeClick(WorkflowEdgeViewModel edge)
    {
        if (edge is null) return;

        SelectedEdge = edge;
        SelectedNode = null;
        Status = LocaleManager.Instance.Format("wpf.workflow.status.edge.selected", edge.Name);
    }

    /// <summary>
    /// Clears the canvas selection state (clicking empty space).
    /// </summary>
    public void ClearSelection()
    {
        SelectedNode = null;
        SelectedEdge = null;
    }

    /// <summary>
    /// Drags a node: updates the canvas position and keeps edge coordinates in sync (edges repaint automatically via PropertyChanged).
    /// </summary>
    public void MoveNode(WorkflowNodeViewModel node, double deltaX, double deltaY)
    {
        if (!IsEditable || node is null) return;

        node.X = Math.Max(0, node.X + deltaX);
        node.Y = Math.Max(0, node.Y + deltaY);
        _positions[node.Node] = (node.X, node.Y);
        UpdateCanvasSize();
    }

    private void ExecuteAddNode()
    {
        if (!IsEditable)
        {
            Status = LocaleManager.Instance.Get("wpf.workflow.status.readonly");
            return;
        }
        if (_nodesArray is null)
        {
            Status = LocaleManager.Instance.Get("wpf.workflow.status.loadfirst");
            return;
        }

        string name = NextNodeName();
        WorkflowNodeViewModel? parent = SelectedNode;
        var data = new WorkflowStepSchema { Name = name };

        // Aligned with addWorkflowNode in schema-node-man: if a node is selected, append as its child node,
        // with the incoming edge expressed via Previous; only when no node is selected add a new entry node.
        if (parent is not null && !string.IsNullOrWhiteSpace(parent.Name))
            data.Previous = [parent.Name];

        IValueAccess? added = _nodesArray.AddRow(null, data);
        if (added is null)
        {
            Status = LocaleManager.Instance.Get("wpf.workflow.status.node.addfailed");
            return;
        }

        _positions[added] = parent is null
            ? (40 + (Nodes.Count % 4) * 220, 40 + (Nodes.Count / 4) * 120)
            : (parent.X + 260, Math.Max(0, parent.Y + 100));
        RebuildGraph(forceStructure: true);
        SelectedNode = Nodes.FirstOrDefault(n => ReferenceEquals(n.Node, added));
        Status = LocaleManager.Instance.Format("wpf.workflow.status.node.added", name);
    }

    private void ExecuteDeleteNode()
    {
        if (!IsEditable)
        {
            Status = LocaleManager.Instance.Get("wpf.workflow.status.readonly");
            return;
        }
        if (_nodesArray is null || SelectedNode is null)
        {
            Status = LocaleManager.Instance.Get("wpf.workflow.status.node.selectfirst");
            return;
        }

        WorkflowNodeViewModel node = SelectedNode;
        string name = node.Name;

        if (Nodes.Any(n => !ReferenceEquals(n, node) &&
                           GetPrevious(n.Node).Any(p => p.Equals(name, StringComparison.OrdinalIgnoreCase))))
        {
            Status = LocaleManager.Instance.Get("wpf.workflow.status.node.delete.haschildren");
            return;
        }

        int index = _nodesArray.IndexOf(node.Node);
        if (index < 0)
        {
            Status = LocaleManager.Instance.Get("wpf.workflow.status.node.notfound");
            return;
        }

        _nodesArray.DelRows(index);
        RebuildGraph(forceStructure: true);
        Status = LocaleManager.Instance.Format("wpf.workflow.status.node.deleted", name);
    }

    private void ExecuteDisconnectEdge()
    {
        if (!IsEditable)
        {
            Status = LocaleManager.Instance.Get("wpf.workflow.status.readonly");
            return;
        }
        if (SelectedEdge is null)
        {
            Status = LocaleManager.Instance.Get("wpf.workflow.status.edge.selectfirst");
            return;
        }

        WorkflowNodeViewModel source = SelectedEdge.Source;
        WorkflowNodeViewModel target = SelectedEdge.Target;
        IValueAccess? previousNode = target.Node.GetAccessValue("previous");

        if (previousNode is not ArrayNode previousArray)
        {
            Status = LocaleManager.Instance.Get("wpf.workflow.error.previous.missing");
            return;
        }

        int index = -1;
        for (int i = 0; i < previousArray.Count; i++)
        {
            IValueAccess? item = previousArray[i] as IValueAccess;
            if (string.Equals(item?.GetValue<string>(), source.Name, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            Status = LocaleManager.Instance.Get("wpf.workflow.status.edge.notfound");
            return;
        }

        previousArray.DelRows(index);
        Status = LocaleManager.Instance.Format("wpf.workflow.status.edge.disconnected", source.Name, target.Name);
        RebuildGraph(forceStructure: false);
    }

    private void TryConnect(WorkflowNodeViewModel source, WorkflowNodeViewModel target)
    {
        string sourceName = source.Name;
        string targetName = target.Name;

        if (string.IsNullOrWhiteSpace(sourceName) || string.IsNullOrWhiteSpace(targetName))
        {
            Status = LocaleManager.Instance.Get("wpf.workflow.error.connect.emptyname");
            return;
        }
        if (sourceName.Equals(targetName, StringComparison.OrdinalIgnoreCase))
        {
            Status = LocaleManager.Instance.Get("wpf.workflow.error.connect.self");
            return;
        }
        if (GetPrevious(target.Node).Any(p => p.Equals(sourceName, StringComparison.OrdinalIgnoreCase)))
        {
            Status = LocaleManager.Instance.Get("wpf.workflow.error.connect.exists");
            return;
        }
        if (PathContains(sourceName, targetName))
        {
            Status = LocaleManager.Instance.Get("wpf.workflow.error.connect.cycle");
            return;
        }

        IValueAccess? previousNode = target.Node.GetAccessValue("previous");
        if (previousNode is not ArrayNode previousArray)
        {
            Status = LocaleManager.Instance.Get("wpf.workflow.error.previous.missing");
            return;
        }

        if (previousArray.AddRow(null, sourceName) is null)
        {
            Status = LocaleManager.Instance.Get("wpf.workflow.error.previous.write");
            return;
        }

        Status = LocaleManager.Instance.Format("wpf.workflow.status.connected", sourceName, targetName);
        RebuildGraph(forceStructure: false);
    }

    /// <summary>
    /// Walks upward along Previous from <paramref name="startName"/> to determine whether <paramref name="goalName"/> is reached.
    /// Used to reject connections that would form a cycle before creating the edge.
    /// </summary>
    private bool PathContains(string startName, string goalName)
    {
        Dictionary<string, WorkflowNodeViewModel> byName = new(StringComparer.OrdinalIgnoreCase);
        foreach (WorkflowNodeViewModel node in Nodes)
            if (!string.IsNullOrWhiteSpace(node.Name) && !byName.ContainsKey(node.Name))
                byName[node.Name] = node;

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<string>();
        pending.Push(startName);

        while (pending.Count > 0)
        {
            string current = pending.Pop();
            if (!visited.Add(current)) continue;
            if (current.Equals(goalName, StringComparison.OrdinalIgnoreCase)) return true;

            if (!byName.TryGetValue(current, out WorkflowNodeViewModel? node)) continue;
            foreach (string previous in GetPrevious(node.Node))
                pending.Push(previous);
        }

        return false;
    }

    private string NextNodeName()
    {
        var names = new HashSet<string>(Nodes.Select(n => n.Name), StringComparer.OrdinalIgnoreCase);
        int index = 1;
        while (names.Contains($"node{index}")) index++;
        return $"node{index}";
    }

    #endregion

    #region Validation / utilities

    private string? ValidateWorkflow()
    {
        if (Nodes.Count == 0) return LocaleManager.Instance.Get("wpf.workflow.error.validate.nonodes");

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (WorkflowNodeViewModel node in Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Name))
                return LocaleManager.Instance.Get("wpf.workflow.error.validate.emptyname");
            if (!names.Add(node.Name))
                return LocaleManager.Instance.Format("wpf.workflow.error.validate.duplicate", node.Name);
        }

        foreach (WorkflowNodeViewModel node in Nodes)
        {
            foreach (string previous in GetPrevious(node.Node))
            {
                if (!names.Contains(previous))
                    return LocaleManager.Instance.Format("wpf.workflow.error.validate.previous.missing", node.Name, previous);
                if (previous.Equals(node.Name, StringComparison.OrdinalIgnoreCase))
                    return LocaleManager.Instance.Format("wpf.workflow.error.validate.self", node.Name);
            }
        }

        int roots = Nodes.Count(n => GetPrevious(n.Node).Length == 0);
        if (roots != 1) return LocaleManager.Instance.Format("wpf.workflow.error.validate.roots", roots);

        var byName = Nodes.ToDictionary(n => n.Name, StringComparer.OrdinalIgnoreCase);
        var states = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        bool HasCycle(string name)
        {
            int state = states.TryGetValue(name, out int value) ? value : 0;
            if (state == 1) return true;
            if (state == 2) return false;

            states[name] = 1;
            foreach (string previous in GetPrevious(byName[name].Node))
                if (HasCycle(previous)) return true;
            states[name] = 2;
            return false;
        }

        foreach (WorkflowNodeViewModel node in Nodes)
            if (HasCycle(node.Name)) return LocaleManager.Instance.Get("wpf.workflow.error.validate.cycle");

        return null;
    }

    private static string[] GetPrevious(IValueAccess node)
    {
        IValueAccess? previous = node.GetAccessValue("previous");
        if (previous is null) return [];
        return previous.GetValue<string[]>() ?? [];
    }

    private static readonly JsonSerializerOptions WorkflowJsonOptions = SchemaNodeJsonExtension.GetSerializerOptions();

    /// <summary>Runtime-only properties that must not be carried into the editing copy.</summary>
    private static readonly string[] RuntimeWorkflowKeys = ["error", "usedBy", "compatibles", "provider"];

    private static WorkflowSchema Clone(WorkflowSchema source)
    {
        // Round-trip through the schema serializer so appended properties (display/description)
        // stored in Extensions are deep-copied as well, and the loaded schema stays untouched
        // until the user saves.
        JsonObject json = JsonSerializer.SerializeToNode(source, WorkflowJsonOptions) as JsonObject
            ?? new JsonObject();

        StripRuntimeState(json);

        return JsonSerializer.Deserialize<WorkflowSchema>(json, WorkflowJsonOptions)
               ?? new WorkflowSchema();
    }

    /// <summary>
    /// Removes runtime-only state from the workflow and each of its nodes. Only the node objects
    /// themselves are touched, so user payload data (for example a state field called "error")
    /// is left intact.
    /// </summary>
    private static void StripRuntimeState(JsonObject root)
    {
        RemoveRuntimeKeys(root);
        if (root["steps"] is JsonArray steps)
            foreach (JsonNode? node in steps)
                if (node is JsonObject nodeObject)
                    RemoveRuntimeKeys(nodeObject);
    }

    private static void RemoveRuntimeKeys(JsonObject target)
    {
        foreach (string key in target.Select(pair => pair.Key)
                     .Where(key => RuntimeWorkflowKeys.Any(runtime => runtime.Equals(key, StringComparison.OrdinalIgnoreCase)))
                     .ToArray())
        {
            target.Remove(key);
        }
    }

    #endregion
}
