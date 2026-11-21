using System.Windows;
using System.Windows.Controls;
using SchemaNode.WPF.View.NodeWrapper;

namespace SchemaNode.WPF.View.Views;

/// <summary>Entry-source / scalar editor. The wrapper is provided as the DataContext.</summary>
public partial class InputView : UserControl
{
    public InputView()
    {
        InitializeComponent();
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        (DataContext as InputWrapper)?.ClearSelection();
    }
}
