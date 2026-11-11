using Avalonia.Controls;

namespace SchemaNode.Avalonia.Test;

/// <summary>Single-window harness shell. Its DataContext is bound explicitly, not auto-wired.</summary>
public partial class MainWindow : Window
{
    /// <summary>Creates the window.</summary>
    public MainWindow()
    {
        InitializeComponent();
    }
}
