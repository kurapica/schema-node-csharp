using System.Windows.Controls;
using Prism.Navigation.Regions;

namespace SchemaNode.WPF.Man.Views;

/// <summary>
/// Type definition document screen (view / edit / new).
/// KeepAlive=false: the screen owns a schema session and its definition node, so the view and its
/// context must be released when navigation moves away.
/// </summary>
[RegionMemberLifetime(KeepAlive = false)]
public partial class TypeDocumentView : UserControl
{
    public TypeDocumentView()
    {
        InitializeComponent();
    }
}
