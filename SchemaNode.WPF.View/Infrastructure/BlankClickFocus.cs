using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using SchemaNode.WPF.View.Views;

namespace SchemaNode.WPF.View;

/// <summary>
/// Moves keyboard focus off a text editor when the user clicks an inert area of a window that hosts
/// schema views. WPF only transfers focus when the click reaches a focusable element, so a click
/// on the background otherwise leaves the editor focused and its LostFocus commit never runs.
///
/// Interactive and data-bearing subtrees are deliberately ignored: editors and buttons keep their
/// normal WPF focus transfer, scroll bars still drag without committing, and table bodies, pickers
/// and the row-editor dialog are never treated as blank space.
/// </summary>
internal static class BlankClickFocus
{
    private static readonly ConditionalWeakTable<Window, Handler> Attached = new();

    /// <summary>Subscribes the window hosting <paramref name="host"/> exactly once.</summary>
    public static void Attach(FrameworkElement host)
    {
        Window? window = Window.GetWindow(host);

        // A popup hosts its content in a separate visual tree (no window), and the row editor
        // dialog keeps its own focus behaviour: clicks inside it must not be treated as blank.
        if (window is null || window is TableRowEditorWindow) return;

        lock (Attached)
        {
            if (Attached.TryGetValue(window, out _)) return;

            Handler handler = new(window);
            Attached.Add(window, handler);
            window.PreviewMouseDown += handler.OnPreviewMouseDown;
        }
    }

    private sealed class Handler
    {
        private readonly Window _window;

        public Handler(Window window) => _window = window;

        public void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;

            // Only a text editor has a LostFocus write to run; a picker commits its selection
            // immediately and does not depend on focus.
            if (Keyboard.FocusedElement is not TextBoxBase editor) return;
            if (Window.GetWindow(editor) != _window) return;

            if (e.OriginalSource is not DependencyObject source) return;
            if (HasInteractiveAncestor(source)) return;

            // Inert area: moving logical focus to the window raises LostFocus on the editor, which
            // runs the normal binding write and validation path. Keyboard.ClearFocus() is not
            // enough here: it drops keyboard focus without raising LostFocus, so the commit never
            // happens.
            FocusManager.SetFocusedElement(_window, _window);
        }

        private bool HasInteractiveAncestor(DependencyObject source)
        {
            for (DependencyObject? current = source;
                 current is not null && !ReferenceEquals(current, _window);
                 current = GetParent(current))
            {
                if (IsInteractive(current)) return true;
            }

            return false;
        }

        private static bool IsInteractive(DependencyObject element) =>
            element is ButtonBase
                or TextBoxBase
                or Selector
                or ScrollBar
                or Thumb
                or DatePicker
                or Calendar
                or MenuItem
                or TableRowGroupView
                or TableView
                or CascaderView
                // Anything that will take focus itself is the click's landing point, not blank
                // space; this keeps collection items and custom focusable hosts out of the path.
                or Control { Focusable: true };

        private static DependencyObject? GetParent(DependencyObject element)
        {
            if (element is Visual or Visual3D) return VisualTreeHelper.GetParent(element);
            if (element is FrameworkContentElement content) return content.Parent;
            return null;
        }
    }
}
