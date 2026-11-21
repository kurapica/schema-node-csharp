using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Prism.Navigation.Regions;
using SchemaNode.WPF.Man.ViewModels;

namespace SchemaNode.WPF.Man.Views;

/// <summary>
/// Workflow configuration view. KeepAlive=false ensures a new view-level SchemaContext is created on every navigation.
///
/// All canvas interaction is delegated to the ViewModel: dragging only updates node coordinates,
/// while edge coordinates are redrawn automatically by WorkflowEdgeViewModel when it observes
/// PropertyChanged on both endpoint nodes, so no extra bindings or event leaks are introduced.
/// </summary>
[RegionMemberLifetime(KeepAlive = false)]
public partial class WorkflowConfigView : UserControl
{
    public WorkflowConfigView()
    {
        InitializeComponent();
    }

    private WorkflowConfigViewModel? ViewModel => DataContext as WorkflowConfigViewModel;

    /// <summary>Drag a node: ignore dragging in connect mode to avoid accidentally changing coordinates.</summary>
    private void OnNodeDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (ViewModel is not { IsConnecting: false } vm) return;
        if (sender is Thumb { DataContext: WorkflowNodeViewModel node })
            vm.MoveNode(node, e.HorizontalChange, e.VerticalChange);
    }

    /// <summary>Node click: select in normal mode, collect start/end points in connect mode.</summary>
    private void OnNodeMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Thumb { DataContext: WorkflowNodeViewModel node })
            ViewModel?.HandleNodeClick(node);
    }

    /// <summary>Edge click: select that edge for use by "Disconnect Edge".</summary>
    private void OnEdgeMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WorkflowEdgeViewModel edge })
        {
            ViewModel?.HandleEdgeClick(edge);
            e.Handled = true;
        }
    }

    /// <summary>Click on empty canvas area: clear selection of nodes and edges.</summary>
    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        ViewModel?.ClearSelection();
    }
}
