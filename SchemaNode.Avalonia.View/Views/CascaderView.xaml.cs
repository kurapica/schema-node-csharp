using System.Diagnostics.CodeAnalysis;
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
/// level, and the closed state shows the complete path. This is the vue cascader form, not a chain
/// of drop-downs. All entry resolution, selection rules and write-back stay in
/// <see cref="InputWrapper"/> and Core.
/// </summary>
/// <remarks>
/// Each row has two hit areas, decided by the wrapper (<c>CanSelectEntry</c>: the Core-provided
/// <c>Disable</c> state plus the non-leaf rule): the label area commits that entry and closes the
/// popup, while the arrow area only opens the next level and leaves the value untouched. A parent
/// that cannot be committed still expands when its label is clicked, so navigating through
/// disabled namespaces keeps working.
/// </remarks>
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

    private bool _committed;
    private DateTime _lastClosedAt = DateTime.MinValue;

    public CascaderView()
    {
        InitializeComponent();
        ApplyFieldMinWidth();

        // Light dismiss and commits both change IsOpen; the observer is the single place that
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
            _committed = false;

            // The field is already a cascader once Core reports a multi-level entry source, but the
            // first candidates may still be in flight. Ask for them again in that case: the popup
            // shows its loading row now and the applied snapshot replaces it in place, so the user
            // never has to click a second time.
            if (Wrapper is { IsCascading: true, IsLoadingEntries: false, HasCascadeEntries: false })
                Wrapper.ReloadCascadeSelection();
            return;
        }

        _lastClosedAt = DateTime.UtcNow;
        if (_committed)
        {
            _committed = false;
            return;
        }

        // Closed without a commit: drop the parent-level selections that were only a preview.
        Wrapper?.ReloadCascadeSelection();
    }

    /// <summary>
    /// Label area of a row: the wrapper commits the entry and reports whether the value was
    /// written, in which case the popup closes. Entries Core marked disabled are not committed;
    /// a parent among them still expands so the path stays navigable.
    /// </summary>
    private void OnItemSelectClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (!TryGetRow(sender, out EntryLevel? level, out EntryItem? item)) return;

        if (Wrapper?.SelectLevelEntry(level, item) != true) return;

        // The value is already written, so closing must not reload the levels.
        _committed = true;
        // Closing inside the click notification is safe: the wrapper already holds the value.
        Dispatcher.UIThread.Post(() => PopupPanel.IsOpen = false);
    }

    /// <summary>Arrow area of a row: opens the next level and never commits the entry itself.</summary>
    private void OnItemExpandClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (!TryGetRow(sender, out EntryLevel? level, out EntryItem? item)) return;

        Wrapper?.ExpandLevelEntry(level, item);
    }

    private static bool TryGetRow(object? sender, [NotNullWhen(true)] out EntryLevel? level, [NotNullWhen(true)] out EntryItem? item)
    {
        level = null;
        item = null;
        if (sender is not Control { DataContext: EntryItem entry } control) return false;
        if (control.Tag is not EntryLevel owner) return false;
        item = entry;
        level = owner;
        return true;
    }

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        Wrapper?.ClearSelection();
        PopupPanel.IsOpen = false;
    }
}
