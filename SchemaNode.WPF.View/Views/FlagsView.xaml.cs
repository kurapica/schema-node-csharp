using System.Windows.Controls;

namespace SchemaNode.WPF.View.Views;

/// <summary>
/// Flags enum multi-select. It shares the input wrapper's candidate/selection model with the
/// regular input view, but flags never fall back to a text box: the value is a membership set.
/// </summary>
public partial class FlagsView : UserControl
{
    public FlagsView()
    {
        InitializeComponent();
    }
}
