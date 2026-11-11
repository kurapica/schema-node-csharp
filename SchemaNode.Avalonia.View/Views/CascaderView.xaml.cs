using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Reactive;
using Avalonia.Threading;
using SchemaNode.Avalonia.View.NodeWrapper;

namespace SchemaNode.Avalonia.View.Views;

/// <summary>
/// Multi-level cascaded selector rendered as side-by-side columns inside one popup: one column per
/// level, clicking a parent opens the next column, clicking a leaf commits the value, and the
/// closed state shows the complete path. This is the vue cascader form, not a chain of drop-downs.
/// </summary>
public partial class CascaderView : UserControl
{
    /// <summary>
    /// Minimum width of the closed path field. A host that measures to content (auto columns,
    /// horizontal stack panels) otherwise collapses the field to its text/padding width, because
    /// the star column falls back to content size during that measure pass.
    /// </summary>
    public static readonly StyledProperty<double> FieldMinWidthProperty =
        AvaloniaProperty.Register<CascaderView, double>(nameof(FieldMinWidth), 0d);

    /// <summary>
    /// A click that dismisses the popup (light dismiss) also reaches the field button; within this
    /// window the click must not reopen the popup, otherwise it would toggle immediately.
    /// </summary>
    private static readonly TimeSpan ReopenGuard = TimeSpan.FromMilliseconds(250);

    private bool _committedLeaf;
    private DateTime _lastClosedAt = DateTime.MinValue;

    public CascaderView()
    {
        InitializeComponent();
        ApplyFieldMinWidth();

        // Light dismiss and leaf commits both change IsOpen; the observer is the single place that
        // decides whether in-progress selections must be discarded.
        PopupPanel.GetObservable(Popup.IsOpenProperty).Subscribe(OnPanelIsOpenChanged);
    }

    /// <summary>Minimum width of the closed path field; 0 keeps it content-sized.</summary>
    public double FieldMinWidth
    {
        get => GetValue(FieldMinWidthProperty);
        set => SetValue(FieldMinWidthProperty, value);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FieldMinWidthProperty)
            ApplyFieldMinWidth();
    }

    private void ApplyFieldMinWidth()
    {
        if (Field is not null)
            Field.MinWidth = FieldMinWidth;
    }

    private InputWrapper? Wrapper => DataContext as InputWrapper;

    private void OnFieldClick(object? sender, RoutedEventArgs e)
    {
        if (DateTime.UtcNow - _lastClosedAt < ReopenGuard) return;
        PopupPanel.IsOpen = !PopupPanel.IsOpen;
    }

    /// <summary>
    /// Mark the press that dismisses the popup. A light dismiss is an outside click; the same
    /// gesture then lands on the field button, so the click handler below must not treat it as a
    /// fresh open request.
    /// </summary>
    private void OnFieldPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (PopupPanel.IsOpen)
            _lastClosedAt = DateTime.UtcNow;
    }

    private void OnFieldKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && PopupPanel.IsOpen)
        {
            _lastClosedAt = DateTime.UtcNow;
            PopupPanel.IsOpen = false;
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down && !PopupPanel.IsOpen)
        {
            PopupPanel.IsOpen = true;
            e.Handled = true;
        }
    }

    private void OnPanelIsOpenChanged(bool isOpen)
    {
        if (isOpen)
        {
            _committedLeaf = false;
            return;
        }

        _lastClosedAt = DateTime.UtcNow;
        if (_committedLeaf) return;

        // Closed without a leaf commit: drop the parent-level selections that were only a preview.
        Wrapper?.ReloadCascadeSelection();
    }

    private void OnColumnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!PopupPanel.IsOpen || _committedLeaf) return;
        if (e.AddedItems.Count == 0 || e.AddedItems[0] is not EntryItem item) return;
        if (item.HasChildren && !(Wrapper?.CanSelectNonLeaf ?? false)) return;

        _committedLeaf = true;
        // Closing inside the selection notification is safe: the wrapper already holds the value.
        Dispatcher.UIThread.Post(() => PopupPanel.IsOpen = false);
    }

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        Wrapper?.ClearSelection();
        PopupPanel.IsOpen = false;
    }
}
