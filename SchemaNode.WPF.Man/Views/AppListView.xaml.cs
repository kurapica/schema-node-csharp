using System.Windows.Controls;
using Prism.Navigation.Regions;

namespace SchemaNode.WPF.Man.Views;

/// <summary>
/// AppListView code-behind.
/// KeepAlive=false: the view and its view-level SchemaContext are removed when navigating away.
/// </summary>
[RegionMemberLifetime(KeepAlive = false)]
public partial class AppListView : UserControl
{
    public AppListView()
    {
        InitializeComponent();
    }
}
