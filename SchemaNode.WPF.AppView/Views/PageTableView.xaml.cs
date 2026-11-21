using System.Windows;
using System.Windows.Controls;
using SchemaNode.WPF.AppView.NodeWrapper;

namespace SchemaNode.WPF.AppView.Views;

/// <summary>
/// Page chrome around the existing <see cref="SchemaNode.WPF.View.Views.TableView"/>: filter bar,
/// page controls and loading overlay. The body itself is the plain table view, so headers,
/// rowspans, sub-tables and the row editor are not duplicated here.
/// </summary>
public partial class PageTableView : UserControl
{
    public PageTableView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is PageTableWrapper wrapper)
            _ = wrapper.EnsureLoadedAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Cancel in-flight loads and release the page held by the host when the surface leaves
        // the tree; the next activation reloads from page zero.
        if (DataContext is PageTableWrapper wrapper)
            wrapper.Deactivate();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // A recycled view can be re-bound to another wrapper (or the same view can be reused after
        // a reload). Release the previous wrapper's page and load through the new instance instead
        // of keeping the stale one behind the bindings.
        if (e.OldValue is PageTableWrapper previous)
            previous.Deactivate();

        if (e.NewValue is PageTableWrapper current && IsLoaded)
            _ = current.EnsureLoadedAsync();
    }
}
