using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using SchemaNode.Node;

namespace SchemaNode.Avalonia.View;

/// <summary>
/// A request to edit one table row through a host-supplied editor. The built-in editor opens a
/// modal window that hosts a <see cref="SchemaView"/> with <see cref="SchemaViewRole.RowEditor"/>,
/// so the row reuses the normal view resolution instead of a hand-built form.
/// </summary>
/// <param name="Node">Row node being edited (the element struct, never a copy).</param>
/// <param name="IsNew">Whether the node was created for an add-row flow and is not yet inserted.</param>
/// <param name="Title">Editor title, already localized.</param>
/// <param name="Owner">Window that should own the editor, when known.</param>
/// <param name="Registry">Registry the editor view should use for nested nodes.</param>
/// <param name="Skin">Skin the editor view resolves through.</param>
public sealed record TableRowEditorRequest(
    DataNode Node,
    bool IsNew,
    string Title,
    Window? Owner,
    SchemaViewRegistry Registry,
    string Skin = SchemaViewRegistry.DefaultSkin)
{
    /// <summary>Owner resolved once: the explicit owner or the application's main window.</summary>
    public Window? EffectiveOwner => Owner ?? MainWindow;

    /// <summary>
    /// Default editor size: about half of the owner, clamped to a usable minimum. Exposed so a
    /// custom editor can size itself exactly like the built-in one.
    /// </summary>
    public Size PreferredSize => TableRowEditorWindowLayout.GetPreferredSize(EffectiveOwner);

    /// <summary>
    /// Applies the built-in editor's size and owner-centered placement to a custom editor window.
    /// </summary>
    public void ApplyLayoutTo(Window window)
        => TableRowEditorWindowLayout.Apply(window, EffectiveOwner);

    private static Window? MainWindow
    {
        get
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                return desktop.MainWindow;
            return null;
        }
    }
}
