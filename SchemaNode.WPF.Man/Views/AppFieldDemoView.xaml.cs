using System.Windows.Controls;
using Prism.Navigation.Regions;

namespace SchemaNode.WPF.Man.Views;

/// <summary>
/// App field demo code-behind. KeepAlive=false gives every navigation a fresh screen-level
/// context and releases the page table's host page source on leave.
/// </summary>
[RegionMemberLifetime(KeepAlive = false)]
public partial class AppFieldDemoView : UserControl
{
    public AppFieldDemoView()
    {
        InitializeComponent();
    }
}
