using System.Windows.Controls;
using Prism.Navigation.Regions;

namespace SchemaNode.WPF.Man.Views;

/// <summary>
/// AppFieldListView code-behind. KeepAlive=false so the per-screen SchemaContext is torn down
/// when the region replaces this view.
/// </summary>
[RegionMemberLifetime(KeepAlive = false)]
public partial class AppFieldListView : UserControl
{
    public AppFieldListView()
    {
        InitializeComponent();
    }
}
