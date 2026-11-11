using System.Windows;

namespace SchemaNode.WPF.View;

/// <summary>
/// Shared size and placement for row editor windows. The built-in editor applies it directly and
/// host-supplied editors can apply the same values through
/// <see cref="TableRowEditorRequest.ApplyLayoutTo"/>, so the registration-level RowEditor hook
/// behaves like the built-in one instead of keeping its own hard-coded size.
/// </summary>
public static class TableRowEditorWindowLayout
{
    /// <summary>Share of the owner's width and height the editor starts with.</summary>
    public const double OwnerRatio = 2.0 / 3.0;

    /// <summary>Fallback minimum width, matching the built-in editor's XAML.</summary>
    public const double DefaultMinimumWidth = 360;

    /// <summary>Fallback minimum height, matching the built-in editor's XAML.</summary>
    public const double DefaultMinimumHeight = 240;

    private const double FallbackWidth = 720;
    private const double FallbackHeight = 560;

    /// <summary>
    /// Size an editor should start with: about two thirds of the owner, clamped to the owner's work
    /// area. Without an owner it falls back to a size that comfortably fits a struct form.
    /// </summary>
    public static Size GetPreferredSize(
        Window? owner,
        double minWidth = DefaultMinimumWidth,
        double minHeight = DefaultMinimumHeight)
    {
        Rect workArea = ResolveWorkArea(owner);
        Rect ownerBounds = ResolveOwnerBounds(owner, workArea);

        double width = owner is null ? FallbackWidth : ownerBounds.Width * OwnerRatio;
        double height = owner is null ? FallbackHeight : ownerBounds.Height * OwnerRatio;

        width = Math.Max(width, minWidth);
        height = Math.Max(height, minHeight);
        width = Math.Min(width, Math.Max(workArea.Width, minWidth));
        height = Math.Min(height, Math.Max(workArea.Height, minHeight));

        return new Size(width, height);
    }

    /// <summary>
    /// Resolves the window that should own a row editor.
    ///
    /// <c>Window.GetWindow</c> returns null for content that is not inside a Window tree (a Popup
    /// host, a detached row template, a recycled container) and an ownerless dialog can end up
    /// behind the shell. The explicit value is kept when it exists; otherwise the active visible
    /// window is used, then the main window, so the editor always stacks above a real top-level
    /// window and every open from the same host behaves the same way.
    /// </summary>
    public static Window? ResolveOwner(Window? preferred)
    {
        if (preferred is not null) return preferred;

        WindowCollection? windows = Application.Current?.Windows;
        if (windows is not null)
        {
            Window? active = windows.OfType<Window>().FirstOrDefault(w => w.IsVisible && w.IsActive);
            if (active is not null) return active;

            Window? visible = windows.OfType<Window>().FirstOrDefault(w => w.IsVisible);
            if (visible is not null) return visible;
        }

        return Application.Current?.MainWindow;
    }

    /// <summary>
    /// Applies the preferred size, centers the window on its owner and keeps it inside the work
    /// area. The window stays resizable; content (not the window) absorbs long forms through its
    /// own scrolling.
    /// </summary>
    public static void Apply(Window window, Window? owner)
    {
        ArgumentNullException.ThrowIfNull(window);

        double minWidth = window.MinWidth > 0 ? window.MinWidth : DefaultMinimumWidth;
        double minHeight = window.MinHeight > 0 ? window.MinHeight : DefaultMinimumHeight;
        Size size = GetPreferredSize(owner, minWidth, minHeight);
        Rect workArea = ResolveWorkArea(owner);
        Rect ownerBounds = ResolveOwnerBounds(owner, workArea);

        double left = ownerBounds.Left + (ownerBounds.Width - size.Width) / 2;
        double top = ownerBounds.Top + (ownerBounds.Height - size.Height) / 2;

        // Keep the whole window inside the work area; only the initial placement is clamped so the
        // user can still resize the window afterwards.
        left = Clamp(left, workArea.Left, workArea.Right - size.Width);
        top = Clamp(top, workArea.Top, workArea.Bottom - size.Height);

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Width = size.Width;
        window.Height = size.Height;
        window.Left = left;
        window.Top = top;
    }

    private static double Clamp(double value, double min, double max)
        => Math.Min(Math.Max(value, min), max);

    /// <summary>
    /// Work area of the monitor the owner occupies. When the owner sits on another monitor the
    /// primary work area would drag the dialog away from it, so the virtual desktop is used instead.
    /// </summary>
    private static Rect ResolveWorkArea(Window? owner)
    {
        Rect workArea = SystemParameters.WorkArea;
        if (owner is null) return workArea;

        Rect ownerBounds = ResolveOwnerBounds(owner, workArea);
        if (workArea.IntersectsWith(ownerBounds)) return workArea;

        return new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);
    }

    private static Rect ResolveOwnerBounds(Window? owner, Rect fallback)
    {
        if (owner is null) return fallback;

        Rect bounds = owner.WindowState == WindowState.Minimized
            ? owner.RestoreBounds
            : new Rect(owner.Left, owner.Top, owner.ActualWidth, owner.ActualHeight);

        if (bounds.Width > 0 && bounds.Height > 0) return bounds;

        bounds = owner.RestoreBounds;
        if (bounds.Width > 0 && bounds.Height > 0) return bounds;

        double width = owner.Width > 0 ? owner.Width : fallback.Width;
        double height = owner.Height > 0 ? owner.Height : fallback.Height;
        return new Rect(
            fallback.Left + (fallback.Width - width) / 2,
            fallback.Top + (fallback.Height - height) / 2,
            width,
            height);
    }
}
