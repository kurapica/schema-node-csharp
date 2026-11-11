using System;
using System.ComponentModel;
using Avalonia;
using SchemaNode.Avalonia.Man.Infrastructure;
using SchemaNode.Runtime;
using SchemaNode.Avalonia.View;

namespace SchemaNode.Avalonia.Man.ViewModels;

/// <summary>
/// A single node (DAG vertex) on the workflow canvas.
///
/// Holds only UI state (position, selection) and a read-only projection of the Core node fields;
/// the actual node data is always held by <see cref="Node"/> (the DataNode of AppWorkflowNodeSchema),
/// and property editing reuses SchemaView, which wraps the node when the panel materializes.
/// </summary>
public sealed class WorkflowNodeViewModel : BindableBase, IDisposable
{
    /// <summary>Node box width (canvas coordinates)</summary>
    public const double NodeWidth = 180;

    /// <summary>Node box height (canvas coordinates)</summary>
    public const double NodeHeight = 56;

    private readonly IValueAccess _node;
    private double _x;
    private double _y;
    private string _name = string.Empty;
    private string _type = string.Empty;
    private bool _isSelected;

    public WorkflowNodeViewModel(IValueAccess node)
    {
        _node = node ?? throw new ArgumentNullException(nameof(node));
        Refresh();
    }

    /// <summary>The Core data node corresponding to this node</summary>
    public IValueAccess Node => _node;

    /// <summary>Node name (Core Name field)</summary>
    public string Name
    {
        get => _name;
        private set => SetProperty(ref _name, value);
    }

    /// <summary>Fully qualified node type (Core Type field)</summary>
    public string Type
    {
        get => _type;
        private set => SetProperty(ref _type, value);
    }

    /// <summary>Canvas X coordinate (top-left)</summary>
    public double X
    {
        get => _x;
        set => SetProperty(ref _x, value);
    }

    /// <summary>Canvas Y coordinate (top-left)</summary>
    public double Y
    {
        get => _y;
        set => SetProperty(ref _y, value);
    }

    /// <summary>Whether it is selected</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>X coordinate of the edge start point</summary>
    public double CenterX => X + NodeWidth / 2;

    /// <summary>Y coordinate of the edge start point</summary>
    public double BottomY => Y + NodeHeight;

    /// <summary>Re-reads the name and type from the Core node (refreshes the canvas text after field edits).</summary>
    public void Refresh()
    {
        Name = _node.GetAccessValue("name")?.GetValue<string>() ?? string.Empty;
        Type = _node.GetAccessValue("type")?.GetValue<string>() ?? string.Empty;
    }

    /// <inheritdoc />
    public void Dispose() { }
}

/// <summary>
/// A directed edge on the workflow canvas.
///
/// The semantics come from Core's <see cref="SchemaNode.Schema.AppWorkflowNodeSchema.Previous"/>:
/// the edge direction is "predecessor → current node", i.e. Target.Previous contains Source.Name.
/// Coordinates are derived from both endpoint node positions, and are automatically repainted via PropertyChanged when a node is dragged.
/// </summary>
public sealed class WorkflowEdgeViewModel : BindableBase, IDisposable
{
    private bool _isSelected;

    public WorkflowEdgeViewModel(WorkflowNodeViewModel source, WorkflowNodeViewModel target)
    {
        Source = source;
        Target = target;
        Source.PropertyChanged += OnNodePropertyChanged;
        Target.PropertyChanged += OnNodePropertyChanged;
    }

    /// <summary>Predecessor node</summary>
    public WorkflowNodeViewModel Source { get; }

    /// <summary>Successor node</summary>
    public WorkflowNodeViewModel Target { get; }

    /// <summary>Edge name used for status messages</summary>
    public string Name => $"{Source.Name} → {Target.Name}";

    /// <summary>Whether it is selected</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public double X1 => Source.CenterX;
    public double Y1 => Source.BottomY;
    public double X2 => Target.CenterX;
    public double Y2 => Target.Y;

    /// <summary>Start point of the drawn edge (Avalonia Line binds points, not X1/Y1).</summary>
    public Point StartPoint => new(X1, Y1);

    /// <summary>End point of the drawn edge.</summary>
    public Point EndPoint => new(X2, Y2);

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(WorkflowNodeViewModel.X) or nameof(WorkflowNodeViewModel.Y)))
            return;

        RaisePropertyChanged(nameof(X1));
        RaisePropertyChanged(nameof(Y1));
        RaisePropertyChanged(nameof(X2));
        RaisePropertyChanged(nameof(Y2));
        RaisePropertyChanged(nameof(StartPoint));
        RaisePropertyChanged(nameof(EndPoint));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Source.PropertyChanged -= OnNodePropertyChanged;
        Target.PropertyChanged -= OnNodePropertyChanged;
    }
}
