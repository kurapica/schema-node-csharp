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
public sealed record TableRowEditorRequest(
    DataNode Node,
    bool IsNew,
    string Title,
    Window? Owner,
    SchemaViewRegistry Registry);
