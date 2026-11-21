using System.Windows;
using SchemaNode.Node;

namespace SchemaNode.WPF.View;

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
/// <param name="Skin">
/// Skin the editor view resolves through. A window is a separate tree and cannot inherit the
/// table's skin, so the current value travels with the request.
/// </param>
public sealed record TableRowEditorRequest(
    DataNode Node,
    bool IsNew,
    string Title,
    Window? Owner,
    SchemaViewRegistry Registry,
    string Skin = SchemaViewRegistry.DefaultSkin)
{
    /// <summary>
    /// Owner used for placement and stacking: the explicit owner, or the current top-level window
    /// when the table lived outside a Window tree (Popup / detached row template). Without that
    /// fallback the dialog has no owner and can be covered by the shell.
    /// </summary>
    public Window? EffectiveOwner => TableRowEditorWindowLayout.ResolveOwner(Owner);

    /// <summary>
    /// Default editor size: about two thirds of the owner, clamped to its work area. Exposed so a
    /// custom editor can size itself exactly like the built-in one.
    /// </summary>
    public Size PreferredSize => TableRowEditorWindowLayout.GetPreferredSize(EffectiveOwner);

    /// <summary>
    /// Applies the built-in editor's size and owner-centered placement to a custom editor window.
    /// </summary>
    public void ApplyLayoutTo(Window window)
        => TableRowEditorWindowLayout.Apply(window, EffectiveOwner);
}
