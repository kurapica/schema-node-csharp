using System.Windows.Controls;
using Prism.Navigation.Regions;

namespace SchemaNode.WPF.Man.Views;

/// <summary>
/// TypeListView code-behind.
/// KeepAlive=false: Prism removes the view when navigating away, so the old view and its view-level SchemaContext are not reused.
/// </summary>
[RegionMemberLifetime(KeepAlive = false)]
public partial class TypeListView : UserControl
{
    public TypeListView()
    {
        InitializeComponent();
    }
}
