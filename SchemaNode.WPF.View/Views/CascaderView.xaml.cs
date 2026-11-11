using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    private static readonly DependencyPropertyDescriptor PopupIsOpenDescriptor =
        DependencyPropertyDescriptor.FromProperty(Popup.IsOpenProperty, typeof(Popup));

    private static CascaderView? _open;

    private bool _committedLeaf;
    private bool _closedByThisPress;

    public CascaderView()
    {
        InitializeComponent();

        // The popup's Closed event is raised in a later dispatcher turn - and is skipped entirely
        // when the popup is reopened within the same gesture - so dismissal must be observed here,
        // synchronously with IsOpen, where the button state of the dismissing press is still known.
        PopupIsOpenDescriptor.AddValueChanged(Panel, OnPanelIsOpenChanged);
    }

    private InputWrapper? Wrapper => DataContext as InputWrapper;

    private void OnViewUnloaded(object sender, RoutedEventArgs e)
    {
        // A control leaving the tree must not stay the static "currently open" instance: a
        // disposed window would otherwise be kept alive and be closed by the next opener.
        if (_open == this) _open = null;
    }

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
        // Consume that Click instead of toggling a second time (see OnPanelIsOpenChanged).
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
        // Template refreshes and level replacements also change the selection while the popup is
        // closed; those must never be treated as a leaf commit, or they would end the interaction
        // before it starts.
        if (!Panel.IsOpen || _committedLeaf) return;
        if (e.AddedItems.Count == 0 || e.AddedItems[0] is not EntryItem item) return;
        if (item.HasChildren && !(Wrapper?.CanSelectNonLeaf ?? false)) return;

        // The wrapper writes the value through the level's Selected binding for leaves and for
        // selectable non-leaves; both commit and close without reloading the levels.
        _committedLeaf = true;
        Panel.IsOpen = false;
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        if (Panel.IsOpen) Panel.IsOpen = false;
        Wrapper?.ClearSelection();
    }

    private void OnPanelIsOpenChanged(object? sender, EventArgs e)
    {
        if (Panel.IsOpen) return;

        if (_open == this) _open = null;

        // A leaf commit has already written the value, so the levels stay as they are.
        if (_committedLeaf)
        {
            _committedLeaf = false;
            return;
        }

        // Dismissed by the popup itself (press outside, where StaysOpen=false closes it). That
        // press is still held when this runs, while the button raises its Click only afterwards.
        // Remember the dismissal so that Click does not reopen the popup.
        if (Mouse.LeftButton == MouseButtonState.Pressed)
            _closedByThisPress = true;

        // Cancelled: discard in-progress parent selections; the node value was never written.
        Wrapper?.ReloadCascadeSelection();
    }
}
