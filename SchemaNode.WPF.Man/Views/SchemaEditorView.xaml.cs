using System.Windows;
using System.Windows.Controls;
using SchemaNode.WPF.View;
using Prism.Mvvm;

namespace SchemaNode.WPF.Man.Views;

/// <summary>
/// Generic schema editor: hands a DataNode to SchemaView, which materializes its wrapper when the editor loads.
/// Reused by pages such as type definition editing and type demos.
/// </summary>
public partial class SchemaEditorView : UserControl
{
    /// <summary>
    /// Hides the built-in "type name + load" bar. The type-definition document screen is opened by
    /// region navigation and already knows its subject, so it turns that bar off.
    /// </summary>
    public static readonly DependencyProperty HideNameBarProperty =
        DependencyProperty.Register(nameof(HideNameBar), typeof(bool), typeof(SchemaEditorView),
            new PropertyMetadata(false));

    /// <summary>Enables the property area; the document screen sets this to false for "查看".</summary>
    public static readonly DependencyProperty IsEditableProperty =
        DependencyProperty.Register(nameof(IsEditable), typeof(bool), typeof(SchemaEditorView),
            new PropertyMetadata(true));

    public SchemaEditorView()
    {
        InitializeComponent();
    }

    public bool HideNameBar
    {
        get => (bool)GetValue(HideNameBarProperty);
        set => SetValue(HideNameBarProperty, value);
    }

    public bool IsEditable
    {
        get => (bool)GetValue(IsEditableProperty);
        set => SetValue(IsEditableProperty, value);
    }
}
