using Avalonia.Controls;

namespace SchemaNode.Avalonia.Man;

/// <summary>Shell window. Its DataContext is assigned by the application host.</summary>
public partial class MainWindow : Window
{
    /// <summary>Creates the shell.</summary>
    public MainWindow()
    {
        InitializeComponent();
    }
}
