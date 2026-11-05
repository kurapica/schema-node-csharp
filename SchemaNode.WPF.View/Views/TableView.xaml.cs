using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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

    /// <summary>
    /// Skin used by this table's cell views. Null (the default) lets the cells inherit the skin in
    /// effect on the table; a value overrides it for the cell subtree only.
    /// </summary>
    public static readonly DependencyProperty CellSkinProperty = DependencyProperty.Register(
        nameof(CellSkin),
        typeof(string),
        typeof(TableView),
        new PropertyMetadata(null, OnSettingsChanged));

    /// <summary>
    /// Effective skin of the hosting <see cref="SchemaView"/>, mirrored here so it can be forwarded
    /// to the row editor window, which is a separate tree and cannot inherit it. Populated by a
    /// binding to the nearest ancestor view rather than by inheritance: a control cannot read a
    /// property it does not own, and an <c>AddOwner</c> alias does not share inherited values.
    /// </summary>
    public static readonly DependencyProperty SkinProperty = DependencyProperty.Register(
        nameof(Skin),
        typeof(string),
        typeof(TableView),
        new PropertyMetadata(SchemaViewRegistry.DefaultSkin, OnSettingsChanged));

    public TableView()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplySettings();
        DataContextChanged += (_, _) => ApplySettings();
        BindingOperations.SetBinding(
            this,
            SkinProperty,
            new Binding(nameof(SchemaView.Skin))
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(SchemaView), 1),
                Mode = BindingMode.OneWay,
            });
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

    /// <summary>Skin used by the cell views; null inherits the table's skin.</summary>
    public string? CellSkin
    {
        get => (string?)GetValue(CellSkinProperty);
        set => SetValue(CellSkinProperty, value);
    }

    /// <summary>Skin inherited from the hosting view, forwarded to the row editor window.</summary>
    public string Skin => (string)GetValue(SkinProperty);

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

        wrapper.CellSkin = CellSkin;
        wrapper.EditorSkin = Skin;

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
