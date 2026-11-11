using Avalonia;
using Avalonia.Controls;

namespace SchemaNode.Avalonia.View;

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

    /// <summary>Fallback minimum width, matching the built-in editor.</summary>
    public const double DefaultMinimumWidth = 360;

    /// <summary>Fallback minimum height, matching the built-in editor.</summary>
    public const double DefaultMinimumHeight = 240;

    private const double FallbackWidth = 720;
    private const double FallbackHeight = 560;

    /// <summary>
    /// Size an editor should start with: about two thirds of the owner, never below the minimum.
    /// Without an owner it falls back to a size that comfortably fits a struct form.
    /// </summary>
    public static Size GetPreferredSize(
        Window? owner,
        double minWidth = DefaultMinimumWidth,
        double minHeight = DefaultMinimumHeight)
    {
        double ownerWidth = owner?.Bounds.Width ?? 0;
        double ownerHeight = owner?.Bounds.Height ?? 0;

        double width = owner is null || ownerWidth <= 0 ? FallbackWidth : ownerWidth * OwnerRatio;
        double height = owner is null || ownerHeight <= 0 ? FallbackHeight : ownerHeight * OwnerRatio;

        return new Size(Math.Max(width, minWidth), Math.Max(height, minHeight));
    }

    /// <summary>
    /// Applies the preferred size and centers the window on its owner. The window stays resizable;
    /// content (not the window) absorbs long forms through its own scrolling.
    /// </summary>
    public static void Apply(Window window, Window? owner)
    {
        ArgumentNullException.ThrowIfNull(window);

        Size size = GetPreferredSize(owner);
        window.Width = size.Width;
        window.Height = size.Height;
        window.WindowStartupLocation = owner is null
            ? WindowStartupLocation.CenterScreen
            : WindowStartupLocation.CenterOwner;
    }
}
