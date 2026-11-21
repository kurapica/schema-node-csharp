using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using SchemaNode.Avalonia.Man.Infrastructure;
using SchemaNode.Avalonia.View;
using SchemaNode.Avalonia.View.Localization;
using SchemaNode.Avalonia.View.NodeWrapper;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;

namespace SchemaNode.Avalonia.Man.NodeWrapper;

/// <summary>One projected row of the workflow step summary.</summary>
public sealed class WorkflowStepSummary
{
    internal WorkflowStepSummary(int index, DataNode node, string name, string type, string[] previous)
    {
        Index = index;
        Node = node;
        Name = name;
        Type = type;
        Previous = previous;
    }

    /// <summary>1-based position in the step array.</summary>
    public int Index { get; }

    /// <summary>Underlying step node, used to locate the row for insert/delete.</summary>
    internal DataNode Node { get; }

    /// <summary>Step name (primary key referenced by <c>previous</c>).</summary>
    public string Name { get; }

    /// <summary>Workflow node type of the step.</summary>
    public string Type { get; }

    /// <summary>Names of the predecessor steps.</summary>
    public string[] Previous { get; }

    /// <summary>Text shown in the first column.</summary>
    public string DisplayName => $"{Index}. {Name}";

    /// <summary>Comma separated predecessor names.</summary>
    public string PreviousText => string.Join(", ", Previous);

    /// <summary>Whether the step declares predecessors.</summary>
    public bool HasPrevious => Previous.Length > 0;
}

/// <summary>
/// Special view for <c>system.schema.workflow.steps</c> (the companion array of
/// <c>system.schema.workflow.step</c>).
///
/// Steps form a small DAG, so the generic table is unusable: it turns every step property into a
/// column (payload, args, fork, fork keys, ...) and the row order says nothing about the graph.
/// This wrapper projects an ordered summary (name / type / predecessors) and, aligned with
/// workflownodesView.vue in schema-node-man, lets the user add/remove steps directly on the array:
/// a new entry node, a child node appended after a selected parent (wired through <c>previous</c>),
/// and delete (blocked while another step still references the target).
/// </summary>
public sealed class WorkflowStepsWrapper : DataNodeWrapper
{
    public WorkflowStepsWrapper(DataNode node, SchemaViewRegistry registry)
        : base(node, registry, null, SchemaViewRole.Field)
    {
        StepsNode = node as ArrayNode
            ?? throw new ArgumentException($"Node '{node.Type.Name}' is not a workflow step list.", nameof(node));

        AddNodeCommand = new DelegateCommand(ExecuteAddNode, () => IsEditable);
        AddChildCommand = new DelegateCommand<WorkflowStepSummary>(ExecuteAddChild, _ => IsEditable);
        DeleteCommand = new DelegateCommand<WorkflowStepSummary>(ExecuteDelete, _ => IsEditable);

        Refresh();
        Track(StepsNode.Subscribe(OnStepsChanged));
    }

    private ArrayNode StepsNode { get; }

    /// <summary>The list is a container; step fields are edited inline here and on the workflow canvas.</summary>
    protected override bool RendersValue => false;

    /// <summary>Step summary in workflow definition order.</summary>
    public BulkObservableCollection<WorkflowStepSummary> Steps { get; } = new();

    /// <summary>Whether the workflow has no steps yet.</summary>
    public bool IsEmpty => Steps.Count == 0;

    /// <summary>Whether the empty-state add button should be shown (editable and no steps yet).</summary>
    public bool ShowEmptyAdd => IsEditable && IsEmpty;

    /// <summary>Localized header with the step count.</summary>
    public string Title => $"{LocaleManager.Instance.Get("avalonia.workflow.steps.title")} ({Steps.Count})";

    /// <summary>Localized empty hint.</summary>
    public string EmptyText => LocaleManager.Instance.Get("avalonia.workflow.steps.empty");

    /// <summary>Localized predecessor label.</summary>
    public string PreviousLabel => LocaleManager.Instance.Get("avalonia.workflow.steps.previous");

    /// <summary>Adds a new entry node (no predecessor).</summary>
    public ICommand AddNodeCommand { get; }

    /// <summary>Adds a child node after the selected row, with <c>previous</c> pointing at it.</summary>
    public ICommand AddChildCommand { get; }

    /// <summary>Removes the selected row (blocked while other steps reference it).</summary>
    public ICommand DeleteCommand { get; }

    private string? _message;

    /// <summary>Inline status/error message (e.g. delete blocked by a referencing step).</summary>
    public string? Message
    {
        get => _message;
        private set
        {
            _message = value;
            RaisePropertyChanged(nameof(Message));
            RaisePropertyChanged(nameof(HasMessage));
        }
    }

    /// <summary>Whether an inline message is currently shown.</summary>
    public bool HasMessage => !string.IsNullOrEmpty(_message);

    /// <inheritdoc />
    protected override void OnEditableChanged()
    {
        base.OnEditableChanged();
        // The base constructor subscribes to ancestor properties with immediate: true, so this
        // hook can fire before the derived constructor body assigns the commands. Guard against
        // the construction-time call instead of dereferencing a null command.
        if (AddNodeCommand is DelegateCommand addNode)
            addNode.RaiseCanExecuteChanged();
        if (AddChildCommand is DelegateCommand<WorkflowStepSummary> addChild)
            addChild.RaiseCanExecuteChanged();
        if (DeleteCommand is DelegateCommand<WorkflowStepSummary> delete)
            delete.RaiseCanExecuteChanged();
        RaisePropertyChanged(nameof(ShowEmptyAdd));
    }

    private void OnStepsChanged((IValueAccess Node, object? Value) args) => Post(Refresh);

    private void Refresh()
    {
        if (IsDisposed) return;

        List<WorkflowStepSummary> next = [];
        int index = 0;
        foreach (IValueAccess item in StepsNode)
        {
            if (item is not DataNode step) continue;
            index++;

            string name = step.GetAccessValue("name")?.GetValue<string>() ?? string.Empty;
            string type = step.GetAccessValue("type")?.GetValue<string>() ?? string.Empty;
            string[] previous = step.GetAccessValue("previous")?.GetValue<string[]>() ?? [];
            next.Add(new WorkflowStepSummary(index, step, name, type, previous));
        }

        Steps.ReplaceAll(next);
        RaisePropertyChanged(nameof(IsEmpty));
        RaisePropertyChanged(nameof(ShowEmptyAdd));
        RaisePropertyChanged(nameof(Title));
    }

    private void ExecuteAddNode()
    {
        if (!IsEditable) return;
        Message = null;
        StepsNode.AddRow(null, new WorkflowStepSchema { Name = NextName(null) });
    }

    private void ExecuteAddChild(WorkflowStepSummary? parent)
    {
        if (!IsEditable || parent is null) return;
        Message = null;

        int index = StepsNode.IndexOf(parent.Node);
        var data = new WorkflowStepSchema
        {
            Name = NextName(parent.Name),
            Previous = [parent.Name]
        };
        StepsNode.AddRow(index >= 0 ? index + 1 : null, data);
    }

    private void ExecuteDelete(WorkflowStepSummary? target)
    {
        if (!IsEditable || target is null) return;
        Message = null;

        // Cannot delete a step that other steps still reference as their predecessor.
        bool referenced = StepsNode
            .OfType<DataNode>()
            .Where(step => !ReferenceEquals(step, target.Node))
            .Select(step => step.GetAccessValue("previous")?.GetValue<string[]>())
            .Any(previous => previous is not null &&
                             previous.Any(p => p.Equals(target.Name, StringComparison.OrdinalIgnoreCase)));
        if (referenced)
        {
            Message = LocaleManager.Instance.Get("avalonia.workflow.steps.delete.haschildren");
            return;
        }

        int index = StepsNode.IndexOf(target.Node);
        if (index >= 0) StepsNode.DelRows(index, 1);
    }

    /// <summary>
    /// Entry nodes are named <c>node{n}</c> (consistent with WorkflowConfigViewModel.NextNodeName);
    /// a child of <paramref name="parent"/> is named <c>{parent}_child{n}</c> (matches
    /// addWorkflowNode in schema-node-man). The first unused index wins.
    /// </summary>
    private string NextName(string? parent)
    {
        var names = new HashSet<string>(Steps.Select(step => step.Name), StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(parent))
        {
            int index = 1;
            while (names.Contains($"node{index}")) index++;
            return $"node{index}";
        }

        int child = 1;
        while (names.Contains($"{parent}_child{child}")) child++;
        return $"{parent}_child{child}";
    }
}
