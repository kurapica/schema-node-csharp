using System.Windows.Controls;
using Prism.Navigation.Regions;

namespace SchemaNode.WPF.Man.Views;

/// <summary>
/// Code-behind for AppDemoView. KeepAlive=false ensures a fresh screen-level context on every navigation.
/// </summary>
[RegionMemberLifetime(KeepAlive = false)]
public partial class AppDemoView : UserControl
{
    public AppDemoView()
    {
        InitializeComponent();
    }
}
