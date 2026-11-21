using SchemaNode.Avalonia.View.NodeWrapper;

namespace SchemaNode.Avalonia.View;

/// <summary>
/// Registration-time options for an array type rendered as a table. A host registers a collection
/// type once and the table machinery applies the options wherever that type appears, so no view
/// needs to recognise the type itself.
///
/// A non-empty <see cref="ViewColumns"/> switches the table to row-editor mode: only the listed
/// field paths are projected as columns, cells render read-only, and the trailing operations column
/// offers row edit (and add-row) through a struct form editor.
/// </summary>
public sealed record SchemaTableOptions
{
    /// <summary>Options for the plain table: every derived column, inline editing.</summary>
    public static readonly SchemaTableOptions Default = new();

    /// <summary>
    /// Field paths to project as columns, including nested paths such as
    /// <c>fieldDefine.display</c>. Empty keeps the derived column set.
    /// </summary>
    public IReadOnlyList<string> ViewColumns { get; init; } = [];

    /// <summary>
    /// Renders the whole table read-only: cells disabled and add/delete hidden. A row editor
    /// enabled by <see cref="ViewColumns"/> stays reachable through the operations column.
    /// </summary>
    public bool ReadOnly { get; init; }

    /// <summary>Skin used by the projected cell views; null inherits the table's skin.</summary>
    public string? CellSkin { get; init; }

    /// <summary>Skin of the row editor view. Null keeps the hosting view's skin.</summary>
    public string? EditorSkin { get; init; }

    /// <summary>
    /// Overrides the row-editor appearance for this collection only. Null keeps the registry-wide
    /// <see cref="SchemaViewRegistry.RowEditorFactory"/> or the built-in struct-form window.
    /// </summary>
    public Func<TableRowEditorRequest, Task<bool>>? RowEditor { get; init; }
}
