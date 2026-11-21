using System.Windows.Controls;
using Prism.Navigation.Regions;

namespace SchemaNode.WPF.Man.Views;

/// <summary>
/// AppWorkflowDefinitionView code-behind. KeepAlive=false so the per-screen SchemaContext is released
/// before the next screen is created.
/// </summary>
[RegionMemberLifetime(KeepAlive = false)]
public partial class AppWorkflowDefinitionView : UserControl
{
    public AppWorkflowDefinitionView()
    {
        InitializeComponent();
    }
}
