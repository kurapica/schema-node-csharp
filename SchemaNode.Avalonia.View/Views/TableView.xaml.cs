using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SchemaNode.Avalonia.View.NodeWrapper;

namespace SchemaNode.Avalonia.View.Views;

/// <summary>
/// Table view: two header rows, a virtualized body of element blocks and in-place width
/// distribution.
///
/// Width handling follows the vue table: the table takes its budget from the nearest ancestor
/// scroll viewer (the horizontal offset is added back, otherwise the measured width shrinks on
/// every scroll and columns ratchet), scales columns up to fill the container, keeps their minimum
/// width when the container shrinks and only then lets the body scroll horizontally. The host's
/// own horizontal scrollbar is disabled so the page never scrolls sideways as a whole.
/// </summary>
public partial class TableView : UserControl
{
    private const double VerticalScrollBarWidth = 12;
    private const double BorderCompensation = 2;

    private TableWrapper? _wrapper;
    private ScrollViewer? _hostScroll;
    private ScrollBarVisibility _hostHorizontal = ScrollBarVisibility.Auto;
    private bool _hostPatched;
    private bool _refreshQueued;
    private double _lastBudget;

    public TableView()
    {
        InitializeComponent();

        this.GetObservable(DataContextProperty).Subscribe(_ => AttachWrapper());
        BodyScroll.GetObservable(ScrollViewer.OffsetProperty).Subscribe(SyncHeader);

        // Layout-driven budget refresh. Avalonia has no LayoutUpdated event, so both the body size
        // and the control's own attachment schedule a deferred pass after layout has settled.
        BodyScroll.SizeChanged += (_, _) => QueueWidthRefresh();
        SizeChanged += (_, _) => QueueWidthRefresh();
    }

    /// <summary>Raised when the shared column budget changed; row views re-apply widths in place.</summary>
    public event EventHandler? AvailableWidthChanged;

    /// <summary>Current horizontal budget used by every row block.</summary>
    public double EffectiveAvailableWidth => _wrapper?.AvailableWidth ?? 0;

    /// <summary>
    /// Column widths computed once by the wrapper. Header cells and body rows both render this
    /// snapshot, so neither side measures columns on its own.
    /// </summary>
    public IReadOnlyList<double> EffectiveColumnWidths =>
        _wrapper?.EffectiveColumnWidths ?? Array.Empty<double>();

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _hostScroll = this.FindAncestorOfType<ScrollViewer>();
        if (_hostScroll is not null && !_hostPatched)
        {
            _hostHorizontal = _hostScroll.HorizontalScrollBarVisibility;
            _hostScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            _hostPatched = true;
        }

        UpdateOwnerWindow();
        QueueWidthRefresh();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (_hostScroll is not null && _hostPatched)
        {
            _hostScroll.HorizontalScrollBarVisibility = _hostHorizontal;
            _hostPatched = false;
        }
        _hostScroll = null;
    }

    private void AttachWrapper()
    {
        TableWrapper? wrapper = DataContext as TableWrapper;
        if (ReferenceEquals(wrapper, _wrapper))
        {
            if (wrapper is not null) ApplySettings();
            return;
        }

        if (_wrapper is not null)
            _wrapper.PropertyChanged -= OnWrapperPropertyChanged;

        _wrapper = wrapper;
        if (_wrapper is null) return;

        // A new wrapper starts without a budget, so the cached value must not suppress the refresh
        // (otherwise a replaced table keeps natural widths while its header uses the container's).
        _lastBudget = 0;
        _wrapper.PropertyChanged += OnWrapperPropertyChanged;
        ApplySettings();
        QueueWidthRefresh();
    }

    /// <summary>
    /// Applies the skin and the owning window. A separate window does not inherit the surrounding
    /// property values, so the table forwards its effective skin into the row-editor request.
    /// </summary>
    private void ApplySettings()
    {
        if (_wrapper is null) return;

        SchemaView? host = this.FindAncestorOfType<SchemaView>();
        if (host is not null)
            _wrapper.EditorSkin = host.Skin;

        UpdateOwnerWindow();
    }

    private void UpdateOwnerWindow()
    {
        if (_wrapper is null) return;
        _wrapper.OwnerWindow = TopLevel.GetTopLevel(this) as Window;
    }

    private void OnWrapperPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TableWrapper.AvailableWidth)) return;
        AvailableWidthChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SyncHeader(Vector offset)
    {
        Vector current = HeaderScroll.Offset;
        if (Math.Abs(current.X - offset.X) < 0.5) return;
        HeaderScroll.Offset = new Vector(offset.X, 0);
    }

    private void QueueWidthRefresh()
    {
        if (_refreshQueued) return;
        _refreshQueued = true;

        Dispatcher.UIThread.Post(
            () =>
            {
                _refreshQueued = false;
                RefreshAvailableWidth();
            },
            DispatcherPriority.Loaded);
    }

    private void RefreshAvailableWidth()
    {
        if (_wrapper is null) return;

        double budget = MeasureBudget();
        if (budget <= 0) return;
        if (Math.Abs(budget - _lastBudget) < 1) return;

        _lastBudget = budget;
        _wrapper.AvailableWidth = budget;
    }

    /// <summary>
    /// Horizontal space available to the table. The table's own scroll viewers are excluded; the
    /// nearest ancestor scroll viewer is the container whose viewport constrains it.
    /// </summary>
    private double MeasureBudget()
    {
        if (_hostScroll is not null)
        {
            Point? origin = this.TranslatePoint(new Point(0, 0), _hostScroll);
            double used = (origin?.X ?? 0) + _hostScroll.Offset.X;
            double budget = _hostScroll.Viewport.Width - used - BorderCompensation;

            if (BodyScroll.VerticalScrollBarVisibility == ScrollBarVisibility.Auto
                && BodyScroll.Extent.Height > BodyScroll.Viewport.Height)
                budget -= VerticalScrollBarWidth;

            return budget;
        }

        double own = Bounds.Width;
        if (own > 0) return own;

        return BodyScroll.Viewport.Width;
    }
}
