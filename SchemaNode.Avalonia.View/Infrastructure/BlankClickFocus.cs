using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using SchemaNode.Avalonia.View.Views;

namespace SchemaNode.Avalonia.View;

/// <summary>
/// Clears input focus when the user presses a blank part of the host window. Text editors commit
/// on LostFocus, so without this a view with a single input could not submit by clicking around it.
///
/// Presses that belong to an interactive surface are left alone: controls that can take focus
/// themselves, scroll bars, table rows/cells, popups, cascade fields, and the row-editor dialog.
/// </summary>
internal static class BlankClickFocus
{
    private static readonly ConditionalWeakTable<Window, object> Attached = new();

    /// <summary>Hooks the window that currently hosts <paramref name="anchor"/>, once per window.</summary>
    public static void Attach(Visual anchor)
    {
        if (TopLevel.GetTopLevel(anchor) is not Window window) return;

        // The row editor is a separate form window; inside it the current field keeps focus.
        if (window is TableRowEditorWindow) return;

        Attached.GetValue(window, static w =>
        {
            w.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed,
                RoutingStrategies.Bubble, handledEventsToo: true);
            return new object();
        });
    }

    private static void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Window window) return;

        // Match the WPF behaviour: only a left click is a blank-click commit. Right-click keeps its
        // context-menu/selection meaning and must not end the edit.
        if (!e.GetCurrentPoint(window).Properties.IsLeftButtonPressed) return;

        // Only a text editor has a LostFocus write to run; pickers commit their selection
        // immediately and do not depend on focus.
        if (window.FocusManager?.GetFocusedElement() is not TextBox) return;

        // A control that already handled the press owns the interaction.
        if (e.Handled) return;
        if (e.Source is not Visual source) return;

        for (Visual? visual = source; visual is not null && visual != window; visual = visual.GetVisualParent())
        {
            // Dragging or clicking a scroll bar must not end the edit.
            if (visual is ScrollBar or Thumb) return;

            // Rows, cells and the table surface keep focus while the user works inside the table.
            if (visual is TableRowGroupView or Views.TableView) return;

            // A cascade field commits through its own popup; its surface is interactive.
            if (visual is CascaderView) return;

            // Collection surfaces (list, combo, selectable groups) own selection and focus.
            if (visual is SelectingItemsControl) return;

            // Popups can share the window through its overlay layer; presses on their content are
            // not blank presses. Native popups live in their own top level and never reach here.
            if (visual is OverlayLayer) return;

            // Text boxes, buttons, combo boxes, list items, and similar controls take focus normally.
            if (visual is InputElement { Focusable: true }) return;
        }

        window.FocusManager?.Focus(null);
    }
}
