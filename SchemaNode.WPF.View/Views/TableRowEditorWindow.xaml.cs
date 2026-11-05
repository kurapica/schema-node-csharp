using System.Windows;
using SchemaNode.Node;

namespace SchemaNode.WPF.View.Views;

/// <summary>
/// Built-in row editor. Hosts a normal <see cref="SchemaView"/> with <see cref="SchemaViewRole.RowEditor"/>
/// so a whole element struct (including nested structs and arrays) is edited through the regular
/// view resolution. Hosts replace it through <see cref="SchemaViewRegistry.RowEditorFactory"/>.
/// </summary>
public partial class TableRowEditorWindow : Window
{
    public TableRowEditorWindow(DataNode node, SchemaViewRegistry registry, string title)
    {
        InitializeComponent();

        Title = title;
        RowView.Registry = registry;
        RowView.Node = node;
    }

    /// <summary>Default editor used when the registry has no <see cref="SchemaViewRegistry.RowEditorFactory"/>.</summary>
    public static bool Show(TableRowEditorRequest request)
    {
        Window? owner = request.Owner ?? Application.Current?.MainWindow;
        TableRowEditorWindow window = new(request.Node, request.Registry, request.Title);

        // The row view lives in its own window, outside the table's inheritance tree, so the
        // request carries the skin explicitly.
        window.RowView.Skin = request.Skin;
        if (owner is not null && owner != window)
            window.Owner = owner;

        return window.ShowDialog() == true;
    }

    private void OnConfirm(object sender, RoutedEventArgs e) => DialogResult = true;
}
