using System.Windows;
using System.Windows.Controls;
using SchemaNode.WPF.View.NodeWrapper;

namespace SchemaNode.WPF.View.Views;

/// <summary>
/// Table editor for arrays of structs. The wrapper projects columns and builds row groups; this
/// view only lays out the two-level header, keeps it aligned with the horizontally scrolled body
/// and forwards host settings to the wrapper.
/// </summary>
public partial class TableView : UserControl
{
    /// <summary>
    /// Comma-separated field-path whitelist, for example <c>name,type,fieldDefine.display</c>.
    /// Null or empty keeps the derived column set. A non-empty value switches the table to
    /// read-only row-editor mode, mirroring vue's <c>viewColumns</c>.
    /// </summary>
    public static readonly DependencyProperty ViewColumnsProperty = DependencyProperty.Register(
        nameof(ViewColumns),
        typeof(string),
        typeof(TableView),
        new PropertyMetadata(null, OnSettingsChanged));

    /// <summary>Renders the table read-only (cells disabled, add/delete hidden).</summary>
    public static readonly DependencyProperty ReadOnlyProperty = DependencyProperty.Register(
        nameof(ReadOnly),
        typeof(bool?),
        typeof(TableView),
        new PropertyMetadata(null, OnSettingsChanged));

    public TableView()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplySettings();
        DataContextChanged += (_, _) => ApplySettings();
    }

    /// <summary>Comma-separated field-path whitelist; null or empty keeps derived columns.</summary>
    public string? ViewColumns
    {
        get => (string?)GetValue(ViewColumnsProperty);
        set => SetValue(ViewColumnsProperty, value);
    }

    /// <summary>Whether the table renders read-only; null leaves the wrapper setting untouched.</summary>
    public bool? ReadOnly
    {
        get => (bool?)GetValue(ReadOnlyProperty);
        set => SetValue(ReadOnlyProperty, value);
    }

    private static void OnSettingsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((TableView)d).ApplySettings();

    private void ApplySettings()
    {
        if (DataContext is not TableWrapper wrapper) return;

        string? paths = ViewColumns;
        if (!string.IsNullOrWhiteSpace(paths))
        {
            wrapper.ViewColumns = paths
                .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToArray();
        }

        if (ReadOnly.HasValue)
            wrapper.ReadOnly = ReadOnly.Value;

        Window? owner = Window.GetWindow(this);
        if (owner is not null) wrapper.OwnerWindow = owner;
    }

    /// <summary>Keeps both header rows aligned with the horizontally scrolled body.</summary>
    private void OnBodyScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.HorizontalChange == 0) return;

        HeaderScroll.ScrollToHorizontalOffset(e.HorizontalOffset);
        TopHeaderScroll.ScrollToHorizontalOffset(e.HorizontalOffset);
    }
}
