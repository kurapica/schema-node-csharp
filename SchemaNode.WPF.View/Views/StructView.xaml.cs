using System.Windows.Controls;

namespace SchemaNode.WPF.View.Views;

/// <summary>Recursive struct form: one row per field, each row re-dispatched through SchemaView.</summary>
public partial class StructView : UserControl
{
    public StructView()
    {
        InitializeComponent();
    }
}
