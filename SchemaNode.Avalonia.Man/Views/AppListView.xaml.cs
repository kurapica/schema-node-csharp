using Avalonia.Controls;

namespace SchemaNode.Avalonia.Man.Views;

/// <summary>App list surface. Its DataContext is a <see cref="ViewModels.AppListViewModel"/>.</summary>
public partial class AppListView : UserControl
{
    /// <summary>Creates the view.</summary>
    public AppListView()
    {
        InitializeComponent();
    }
}
