using SchemaNode.Node;
using SchemaNode.WPF.View;
using SchemaNode.WPF.View.Localization;
using SchemaNode.WPF.View.NodeWrapper;

namespace SchemaNode.WPF.Man.NodeWrapper;

/// <summary>
/// Enum value table (counterpart of enumValues.vue): the order of entries is meaningful, so each
/// row gets a "move up" action in addition to the inherited edit/delete actions.
///
/// Ordering is a Man editing concern, not a generic table capability, which is why this wrapper
/// lives here instead of adding an operation to <see cref="TableWrapper"/>.
/// </summary>
public sealed class EnumValuesTableWrapper : TableWrapper
{
    public EnumValuesTableWrapper(DataNode node, SchemaViewRegistry? registry, string? fieldName, SchemaViewRole role,
                                  SchemaTableOptions? options = null)
        : base(node, registry, fieldName, role, options)
    {
    }

    /// <inheritdoc />
    protected override TableCell BuildRowOperationCell(DataNode element, int columnIndex, int visualRows)
    {
        TableCell cell = base.BuildRowOperationCell(element, columnIndex, visualRows);
        if (!CanMoveUp(element)) return cell;

        List<TableCellAction> actions =
        [
            new TableCellAction(
                LocaleManager.Instance.Get("wpf.man.enum.moveup"),
                new RelayCommand(_ => MoveUp(element), _ => CanMoveUp(element))),
            .. cell.Actions,
        ];
        return new TableCell(columnIndex, 0, visualRows, TableCellKind.RowOperation, null, actions);
    }

    private bool CanMoveUp(DataNode element)
        => !IsDisposed && IsEditable && ArrayNodeImpl.IndexOf(element) > 0;

    private void MoveUp(DataNode element)
    {
        if (!CanMoveUp(element)) return;
        int index = ArrayNodeImpl.IndexOf(element);
        ArrayNodeImpl.MoveRow(index, index - 1);
    }
}
