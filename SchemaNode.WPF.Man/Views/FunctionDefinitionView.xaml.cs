using System.Windows.Controls;
using Prism.Navigation.Regions;

namespace SchemaNode.WPF.Man.Views;

/// <summary>
/// Function definition screen. KeepAlive=false ensures a new screen-level SchemaContext is
/// created on every navigation.
/// </summary>
[RegionMemberLifetime(KeepAlive = false)]
public partial class FunctionDefinitionView : UserControl
{
    public FunctionDefinitionView()
    {
        InitializeComponent();
    }
}
