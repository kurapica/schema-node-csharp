using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SchemaNode.WPF.View.NodeWrapper;

namespace SchemaNode.WPF.View.Views;

/// <summary>
/// Multi-level entry selector: one closed field for the whole path, and a side-by-side popup
/// with one column per level. It only reads/writes the wrapper's level data, so all entry
/// resolution and write-back stay in <see cref="InputWrapper"/> and Core.
/// </summary>
/// <remarks>
/// Commit/expand rule: clicking an item with children expands (the wrapper loads the next level
/// and keeps the popup open); an item's value is committed exactly when the wrapper considers it
/// selectable (leaf, <c>LeafOnly</c>, or a selectable non-leaf). Clicking a leaf closes the popup.
/// Esc or an outside click closes without committing further, and the wrapper reloads the levels
/// from its unchanged node value.
/// </remarks>
public partial class CascaderView : UserControl
{
    private static CascaderView? _open;

    private bool _committedLeaf;
    private bool _closedByThisPress;

    public CascaderView()
    {
        InitializeComponent();
    }

    private InputWrapper? Wrapper => DataContext as InputWrapper;

    /// <summary>
    /// Opens/closes the popup from the field's <c>Click</c> (mouse release or Enter/Space), never
    /// from a mouse-down.
    /// </summary>
    /// <remarks>
    /// <c>StaysOpen=false</c> gives the popup root mouse capture as soon as it opens and closes it
    /// on any mouse press/release outside its bounds (Popup.OnPreviewMouseButton checks both down
    /// and up). Opening on mouse-down therefore makes the release of the very click that opened it
    /// hit-test outside and dismiss the popup again. Opening on release (Click) is the last event
    /// of that gesture, so nothing of the opening gesture can close it.
    /// </remarks>
    private void OnFieldClick(object sender, RoutedEventArgs e)
    {
        if (Wrapper is not { IsEditable: true }) return;

        // A click on the field while the popup is open is dismissed by the popup's own
        // outside-press handling, but the button still completes that gesture and raises Click.
        // Consume that Click instead of toggling a second time (see OnPanelClosed).
        if (_closedByThisPress)
        {
            _closedByThisPress = false;
            return;
        }

        if (Panel.IsOpen)
        {
            Panel.IsOpen = false;
            return;
        }

        _committedLeaf = false;
        if (_open is { } other && other != this) other.Panel.IsOpen = false;
        _open = this;
        Panel.IsOpen = true;
    }

    private void OnFieldPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        // A press that reaches the field starts a gesture with the popup already closed, so any
        // earlier dismissal must not suppress this gesture's Click.
        _closedByThisPress = false;
    }

    private void OnFieldPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // A keyboard gesture neither dismisses nor completes a mouse gesture.
        _closedByThisPress = false;

        if (e.Key != Key.Escape || !Panel.IsOpen) return;
        e.Handled = true;
        Panel.IsOpen = false;
    }

    private void OnPanelPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        e.Handled = true;
        Panel.IsOpen = false;
    }

    private void OnColumnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox { SelectedItem: EntryItem item }) return;
        if (item.HasChildren) return;

        // A leaf commit: the wrapper has already written the value through the level's Selected
        // binding; close without reloading the levels.
        _committedLeaf = true;
        Panel.IsOpen = false;
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        if (Panel.IsOpen) Panel.IsOpen = false;
        Wrapper?.ClearCascade();
    }

    private void OnPanelClosed(object? sender, EventArgs e)
    {
        if (_open == this) _open = null;

        if (_committedLeaf)
        {
            _committedLeaf = false;
            return;
        }

        // The popup dismisses itself on the press that lands outside it, before the press gesture
        // completes. The field's button still finishes that gesture with a Click (the raised
        // press re-targets the button once the popup releases capture), so remember the dismissal
        // to keep OnFieldClick from reopening the popup from the very same click.
        _closedByThisPress = Mouse.LeftButton == MouseButtonState.Pressed;

        // Cancelled: discard in-progress parent selections; the node value was never written.
        Wrapper?.ReloadCascadeSelection();
    }
}
