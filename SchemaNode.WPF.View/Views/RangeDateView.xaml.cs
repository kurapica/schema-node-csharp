using System.Windows.Controls;

namespace SchemaNode.WPF.View.Views;

/// <summary>
/// Side-by-side start/end editor for the range date types. The wrapper stays the generic
/// <see cref="NodeWrapper.StructWrapper"/>; only the layout is type-specific.
/// </summary>
public partial class RangeDateView : UserControl
{
    public RangeDateView()
    {
        InitializeComponent();
    }
}
