using System.Windows;
using SchemaNode.Node;
using SchemaNode.WPF.View.Localization;

namespace SchemaNode.WPF.View.Views;

/// <summary>
/// Built-in row editor. Hosts a normal <see cref="SchemaView"/> with <see cref="SchemaViewRole.RowEditor"/>
/// so a whole element struct (including nested structs and arrays) is edited through the regular
/// view resolution. Hosts replace it through <see cref="SchemaViewRegistry.RowEditorFactory"/>.
/// </summary>
public partial class TableRowEditorWindow : Window
{
    private readonly DataNode _node;
    private bool _confirming;

    public TableRowEditorWindow(DataNode node, SchemaViewRegistry registry, string title)
    {
        InitializeComponent();

        _node = node;
        Title = title;
        RowView.Registry = registry;
        RowView.Node = node;
    }

    /// <summary>Default editor used when the registry has no <see cref="SchemaViewRegistry.RowEditorFactory"/>.</summary>
    public static bool Show(TableRowEditorRequest request)
    {
        Window? owner = request.EffectiveOwner;
        TableRowEditorWindow window = new(request.Node, request.Registry, request.Title);

        // The row view lives in its own window, outside the table's inheritance tree, so the
        // request carries the skin explicitly.
        window.RowView.Skin = request.Skin;
        if (owner is not null && owner != window)
            window.Owner = owner;

        // Two thirds of the owner, centered and clamped to the work area. The request exposes the
        // same values to custom editors, so both paths share one sizing rule.
        request.ApplyLayoutTo(window);

        // Modal: an owned dialog blocks the shell while it is open, so every table (including the
        // ones rendered in a row template or inside a dialog) has a stable stacking order.
        return window.ShowDialog() == true;
    }

    private async void OnConfirm(object sender, RoutedEventArgs e)
    {
        if (_confirming) return;
        _confirming = true;
        try
        {
            // Validation is asynchronous in Core; force the whole subtree through it so a value
            // typed just before clicking OK cannot slip through on a pending result.
            if (!await RowEditorValidation.ValidateTreeAsync(_node))
            {
                ValidationError.Text = LocaleManager.Instance.Get("wpf.table.error.invalid");
                ValidationError.Visibility = Visibility.Visible;
                return;
            }

            DialogResult = true;
        }
        finally
        {
            _confirming = false;
        }
    }
}
