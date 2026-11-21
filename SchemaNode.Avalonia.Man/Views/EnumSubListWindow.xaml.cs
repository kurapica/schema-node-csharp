using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SchemaNode.Avalonia.Man.Views;

/// <summary>Modal child-entry editor opened from the enum values table's "next level" action.</summary>
public partial class EnumSubListWindow : Window
{
    public EnumSubListWindow()
    {
        InitializeComponent();
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
