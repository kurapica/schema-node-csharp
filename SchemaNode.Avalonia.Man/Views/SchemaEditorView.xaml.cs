using Avalonia;
using Avalonia.Controls;

namespace SchemaNode.Avalonia.Man.Views;

/// <summary>
/// Generic schema editor: hands a <c>DataNode</c> to <see cref="SchemaNode.Avalonia.View.SchemaView"/>,
/// which materializes its wrapper when the editor loads. Reused by the type document screen and the
/// type demo screen.
/// </summary>
public partial class SchemaEditorView : UserControl
{
    /// <summary>Hides the built-in "type name + load" bar.</summary>
    public static readonly StyledProperty<bool> HideNameBarProperty =
        AvaloniaProperty.Register<SchemaEditorView, bool>(nameof(HideNameBar));

    /// <summary>Enables the property area; the document screen sets this to false for "view".</summary>
    public static readonly StyledProperty<bool> IsEditableProperty =
        AvaloniaProperty.Register<SchemaEditorView, bool>(nameof(IsEditable), defaultValue: true);

    /// <summary>Creates the view.</summary>
    public SchemaEditorView()
    {
        InitializeComponent();
    }

    /// <summary>Hides the built-in "type name + load" bar.</summary>
    public bool HideNameBar
    {
        get => GetValue(HideNameBarProperty);
        set => SetValue(HideNameBarProperty, value);
    }

    /// <summary>Enables or disables the property area.</summary>
    public bool IsEditable
    {
        get => GetValue(IsEditableProperty);
        set => SetValue(IsEditableProperty, value);
    }
}
