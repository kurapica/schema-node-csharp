using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using SchemaNode.Avalonia.Man.ViewModels;

namespace SchemaNode.Avalonia.Man.Views;

/// <summary>
/// Workflow configuration view. All canvas interaction is delegated to the ViewModel: dragging
/// only updates node coordinates, while edge coordinates are redrawn by WorkflowEdgeViewModel when
/// it observes PropertyChanged on both endpoint nodes.
/// </summary>
public partial class WorkflowConfigView : UserControl
{
    private WorkflowConfigViewModel? _viewModel;

    public WorkflowConfigView()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        base.OnDataContextChanged(e);

        _viewModel = DataContext as WorkflowConfigViewModel;
        if (_viewModel is not null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        ApplyZoom();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WorkflowConfigViewModel.Zoom))
            ApplyZoom();
    }

    /// <summary>Avalonia has no LayoutTransform; render scaling keeps canvas coordinates unchanged.</summary>
    private void ApplyZoom()
    {
        double zoom = _viewModel?.Zoom ?? 1.0;
        GraphCanvas.RenderTransform = new ScaleTransform(zoom, zoom);
    }

    /// <summary>Drag a node: ignore dragging in connect mode to avoid accidentally changing coordinates.</summary>
    private void OnNodeDragDelta(object? sender, VectorEventArgs e)
    {
        if (_viewModel is not { IsConnecting: false } viewModel) return;
        if (sender is Thumb { DataContext: WorkflowNodeViewModel node })
            viewModel.MoveNode(node, e.Vector.X, e.Vector.Y);
    }

    /// <summary>Node click: select in normal mode, collect start/end points in connect mode.</summary>
    private void OnNodePressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Thumb { DataContext: WorkflowNodeViewModel node })
        {
            _viewModel?.HandleNodeClick(node);
            e.Handled = true;
        }
    }

    /// <summary>Edge click: select that edge for use by "Disconnect Edge".</summary>
    private void OnEdgePressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: WorkflowEdgeViewModel edge })
        {
            _viewModel?.HandleEdgeClick(edge);
            e.Handled = true;
        }
    }

    /// <summary>Click on empty canvas area: clear selection of nodes and edges.</summary>
    private void OnCanvasPressed(object? sender, PointerPressedEventArgs e) => _viewModel?.ClearSelection();
}
