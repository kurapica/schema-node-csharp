using System.Windows.Controls;

namespace SchemaNode.WPF.View.Views;

/// <summary>
/// Table editor for arrays of structs. Rows and cells are data-templated from the wrapper; the
/// row list is virtualized and every cell keeps a fixed one-line height.
/// </summary>
public partial class TableView : UserControl
{
    public TableView()
    {
        InitializeComponent();
    }

    /// <summary>Keeps the header columns aligned with the horizontally scrolled body.</summary>
    private void OnBodyScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.HorizontalChange != 0)
            HeaderScroll.ScrollToHorizontalOffset(e.HorizontalOffset);
    }
}
