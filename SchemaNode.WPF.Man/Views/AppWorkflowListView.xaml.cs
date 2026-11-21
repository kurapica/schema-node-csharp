using System.Windows.Controls;
using Prism.Navigation.Regions;

namespace SchemaNode.WPF.Man.Views;

/// <summary>
/// AppWorkflowListView code-behind. KeepAlive=false so the per-screen SchemaContext is released on leave.
/// </summary>
[RegionMemberLifetime(KeepAlive = false)]
public partial class AppWorkflowListView : UserControl
{
    public AppWorkflowListView()
    {
        InitializeComponent();
    }
}
