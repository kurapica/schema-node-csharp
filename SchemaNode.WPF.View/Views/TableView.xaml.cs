using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
    /// to the row editor window, which is a separate tree and cannot inherit it. Resolved from the
    /// nearest ancestor view in <see cref="SyncSkinFromHost"/> rather than by a binding: a control
    /// cannot read a property it does not own, and a <c>FindAncestor</c> binding reports an error
    /// whenever the table is hosted without a <see cref="SchemaView"/> ancestor (custom editors and
    /// popup trees). Without such an ancestor the property keeps the default skin.
    /// </summary>
    public static readonly DependencyProperty SkinProperty = DependencyProperty.Register(
        nameof(Skin),
        typeof(string),
        typeof(TableView),
        new PropertyMetadata(SchemaViewRegistry.DefaultSkin, OnSettingsChanged));

    private static readonly DependencyPropertyDescriptor HostSkinDescriptor =
        DependencyPropertyDescriptor.FromProperty(SchemaView.SkinProperty, typeof(SchemaView))!;

    private double _viewportWidth = -1;
    private SchemaView? _skinHost;
    private ScrollViewer? _hostScroll;
    private ScrollBarVisibility _hostHorizontal = ScrollBarVisibility.Auto;
    private bool _hostPatched;

    public TableView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            DisableHostHorizontalScroll();
            SyncSkinFromHost();
            ApplySettings();
        };

        // The table leaves the tree when the wrapper subtree is replaced or a row is recycled,
        // which is also where the subscription to the old host has to be dropped and the host's
        // own horizontal scrolling has to be handed back.
        Unloaded += (_, _) =>
        {
            RestoreHostHorizontalScroll();
            DetachSkinHost();
        };
        DataContextChanged += (_, _) => ApplySettings();

        // The body viewport only exists after a layout pass, and swapping the table's content
        // doesn't change the viewport itself - so a per-pass check is the reliable trigger for
        // handing the columns their available width.
        LayoutUpdated += (_, _) => RefreshAvailableWidth();
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

        // Null means the cells keep the inherited skin, so a registration-provided cell skin is
        // not cleared by a host that simply did not set one.
        if (CellSkin is not null) wrapper.CellSkin = CellSkin;
        wrapper.EditorSkin = Skin;

        // Content hosted outside a Window tree (Popup, detached row template) has no window
        // ancestor; the resolver falls back to the active/main window so the row editor is always
        // owned by a real top-level window and cannot open behind the shell.
        wrapper.OwnerWindow = TableRowEditorWindowLayout.ResolveOwner(Window.GetWindow(this));

        // A new table must be measured again even when the host keeps the same viewport width.
        _viewportWidth = -1;
        RefreshAvailableWidth();
    }

    /// <summary>
    /// Mirrors the skin of the nearest ancestor <see cref="SchemaView"/>, or the default skin when
    /// there is none (a custom row editor or a popup tree). A code lookup replaces the
    /// <c>FindAncestor</c> binding so an absent ancestor is a normal case instead of a binding
    /// error, and the subscription keeps a later change on the host in sync.
    /// </summary>
    private void SyncSkinFromHost()
    {
        SchemaView? host = FindAncestorSchemaView();
        if (!ReferenceEquals(host, _skinHost))
        {
            DetachSkinHost();
            _skinHost = host;
            if (host is not null)
                HostSkinDescriptor.AddValueChanged(host, OnHostSkinChanged);
        }

        SetValue(SkinProperty, host?.Skin ?? SchemaViewRegistry.DefaultSkin);
    }

    private void OnHostSkinChanged(object? sender, EventArgs e)
        => SetValue(SkinProperty, _skinHost?.Skin ?? SchemaViewRegistry.DefaultSkin);

    private void DetachSkinHost()
    {
        if (_skinHost is null) return;
        HostSkinDescriptor.RemoveValueChanged(_skinHost, OnHostSkinChanged);
        _skinHost = null;
    }

    /// <summary>Nearest ancestor view, or null when the table is hosted outside the view tree.</summary>
    private SchemaView? FindAncestorSchemaView()
    {
        DependencyObject? current = VisualTreeHelper.GetParent(this);
        while (current is not null)
        {
            if (current is SchemaView view) return view;
            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    /// <summary>
    /// Keeps both header rows aligned with the horizontally scrolled body.
    /// </summary>
    private void OnBodyScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        RefreshAvailableWidth();
        if (e.HorizontalChange == 0) return;

        HeaderScroll.ScrollToHorizontalOffset(e.HorizontalOffset);
        TopHeaderScroll.ScrollToHorizontalOffset(e.HorizontalOffset);
    }

    private void OnBodySizeChanged(object sender, SizeChangedEventArgs e)
        => RefreshAvailableWidth();

    /// <summary>
    /// Hands the table the space its columns may occupy. The budget must come from outside this
    /// control: the body <see cref="ScrollViewer"/> measures its content with an infinite width
    /// whenever the host scrolls horizontally, so its own viewport grows with the content and can
    /// never be used as the available width (that is what made the table overflow without bound).
    /// </summary>
    private void RefreshAvailableWidth()
    {
        if (DataContext is not TableWrapper wrapper) return;

        double width = MeasureBudget();
        if (width <= 0) return;
        if (Math.Abs(width - _viewportWidth) < 1) return;

        _viewportWidth = width;

        // Pinning the table to the budget keeps the host from measuring content at its natural
        // width, which is what turns the page-wide horizontal scrollbar into table-local scrolling.
        // Width is NaN (auto-sized) until the first pin and every comparison with NaN is false,
        // so NaN must count as "not pinned yet" or the table would never be constrained.
        if (double.IsNaN(Width) || Math.Abs(Width - width) >= 0.5) Width = width;

        wrapper.AvailableWidth = width;
    }

    /// <summary>
    /// Space this table may occupy inside the nearest ancestor scroll host. The host's viewport
    /// minus the table's offset in it is stable under horizontal scrolling (offset and position
    /// move together), so a resize cannot feed back into itself.
    /// </summary>
    private double MeasureBudget()
    {
        double width;

        ScrollViewer? host = FindHostScroll();
        if (host is not null)
        {
            try
            {
                Point origin = TransformToAncestor(host).Transform(new Point(0, 0));

                // The transform follows the scrolled content, so the offset is added back to get
                // the table's unscrolled position; without this the budget would grow while the
                // user scrolls horizontally and feed back into the column widths.
                origin.X += host.HorizontalOffset;
                width = Math.Min(host.ViewportWidth, host.ViewportWidth - origin.X);
            }
            catch (InvalidOperationException)
            {
                width = 0;
            }
        }
        else
        {
            width = BodyScroll.ViewportWidth;
        }

        if (width <= 0)
            width = ActualWidth > 0 ? ActualWidth : 0;

        // The body's vertical scrollbar takes width away from the columns but not from the header.
        if (width > 0 && BodyScroll.ComputedVerticalScrollBarVisibility == Visibility.Visible)
            width -= SystemParameters.VerticalScrollBarWidth;

        return Math.Max(width, 0);
    }

    /// <summary>Nearest ancestor scroll host, or null when the table is laid out at a fixed width.</summary>
    private ScrollViewer? FindHostScroll()
    {
        DependencyObject? current = VisualTreeHelper.GetParent(this);
        while (current is not null)
        {
            if (current is ScrollViewer viewer) return viewer;
            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    /// <summary>
    /// A host scroll viewer measures its content with an infinite width whenever it may scroll
    /// horizontally, which is exactly what let the table grow without bound and produced the
    /// page-wide scrollbar. The table compresses its columns to their minimums and scrolls inside
    /// its own body instead, so horizontal scrolling on the host can only ever be a feedback loop
    /// and is turned off; the vertical budget is unaffected.
    /// </summary>
    private void DisableHostHorizontalScroll()
    {
        _hostScroll = FindHostScroll();
        if (_hostScroll is null || _hostPatched) return;

        // Remember the host's original value so leaving the tree does not leave the host with a
        // permanently disabled scrollbar.
        _hostHorizontal = _hostScroll.HorizontalScrollBarVisibility;
        _hostScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        _hostPatched = true;
    }

    /// <summary>
    /// Hands the host back the horizontal scrolling it had before the table patched it.
    /// </summary>
    private void RestoreHostHorizontalScroll()
    {
        if (_hostScroll is not null && _hostPatched)
        {
            _hostScroll.HorizontalScrollBarVisibility = _hostHorizontal;
            _hostPatched = false;
        }

        _hostScroll = null;
    }
}
