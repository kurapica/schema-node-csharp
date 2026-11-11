using Avalonia;
using Avalonia.Controls;
using SchemaNode.Avalonia.AppView.NodeWrapper;

namespace SchemaNode.Avalonia.AppView.Views;

/// <summary>
/// Page chrome around the existing <see cref="SchemaNode.Avalonia.View.Views.TableView"/>: filter
/// bar, page controls and loading overlay. The body itself is the plain table view, so headers,
/// rowspans, sub-tables and the row editor are not duplicated here.
/// </summary>
public partial class PageTableView : UserControl
{
    private PageTableWrapper? _dataContextWrapper;
    private bool _attached;

    public PageTableView()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _attached = true;
        _dataContextWrapper = DataContext as PageTableWrapper;
        if (_dataContextWrapper is not null)
            _ = _dataContextWrapper.EnsureLoadedAsync();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _attached = false;

        // Cancel in-flight loads and release the page held by the host when the surface leaves
        // the tree; the next activation reloads from page zero.
        _dataContextWrapper?.Deactivate();
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        // A recycled view can be re-bound to another wrapper (or the same view can be reused after
        // a reload). Release the previous wrapper's page and load through the new instance instead
        // of keeping the stale one behind the bindings.
        PageTableWrapper? current = DataContext as PageTableWrapper;
        if (!ReferenceEquals(_dataContextWrapper, current))
            _dataContextWrapper?.Deactivate();

        _dataContextWrapper = current;
        if (current is not null && _attached)
            _ = current.EnsureLoadedAsync();
    }
}
