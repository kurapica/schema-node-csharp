using System.Windows.Controls;
using Prism.Navigation.Regions;

namespace SchemaNode.WPF.Man.Views;

/// <summary>
/// AppDefinitionView code-behind. KeepAlive=false ensures a new screen-level context is used
/// on every navigation.
/// </summary>
[RegionMemberLifetime(KeepAlive = false)]
public partial class AppDefinitionView : UserControl
{
    public AppDefinitionView()
    {
        InitializeComponent();
    }
}
