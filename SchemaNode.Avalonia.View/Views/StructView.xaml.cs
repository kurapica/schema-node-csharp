using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace SchemaNode.Avalonia.View.Views;

/// <summary>Recursive struct form: one row per field, each row re-dispatched through SchemaView.</summary>
public partial class StructView : UserControl
{
    /// <summary>
    /// Height up to which a field is treated as one editor line. Editors are 24 px high (they set
    /// MinHeight 24); anything taller is an expanded multi-row field and gets a top-aligned title.
    /// </summary>
    private const double SingleLineHeight = 30;

    public StructView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Keeps the single-line appearance (title centered next to its editor) while moving the title
    /// to the top-left as soon as the field renders more than one row, e.g. a nested struct or a
    /// struct array.
    /// </summary>
    private void OnFieldSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (sender is not Control field) return;
        if (field.Parent is not Grid row) return;

        TextBlock? title = row.Children.OfType<TextBlock>().FirstOrDefault();
        if (title is null) return;

        title.VerticalAlignment = e.NewSize.Height > SingleLineHeight
            ? VerticalAlignment.Top
            : VerticalAlignment.Center;
    }
}
