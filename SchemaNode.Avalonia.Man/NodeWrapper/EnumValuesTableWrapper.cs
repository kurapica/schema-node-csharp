using System.Windows.Input;
using Avalonia.Controls;
using SchemaNode.Avalonia.Man.Views;
using SchemaNode.Avalonia.View;
using SchemaNode.Avalonia.View.Localization;
using SchemaNode.Avalonia.View.NodeWrapper;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property.Common;
using SchemaNode.Runtime;

namespace SchemaNode.Avalonia.Man.NodeWrapper;

/// <summary>
/// Enum <c>values</c> table for the Man host: the generic table plus the two row operations
/// mirroring schema-node-man/src/view/enumValues.vue.
///
///   - "move up" reorders the entry list without leaving the screen;
///   - "next level" opens the cascade child editor for that parent entry (only when Core marks the
///     value read-only, i.e. a custom enum entry backed by a provider, and the enum has cascade).
///
/// Both actions are registered here, never in the generic table wrapper.
/// </summary>
public sealed class EnumValuesTableWrapper : TableWrapper
{
    private EnumValuesEditorViewModelHandle? _handle;

    public EnumValuesTableWrapper(
        DataNode node,
        SchemaViewRegistry? registry,
        string? fieldName,
        SchemaViewRole role,
        SchemaTableOptions? options = null)
        : base(node, registry, fieldName, role, options)
    {
    }

    /// <inheritdoc />
    protected override TableCell BuildRowOperationCell(DataNode element, int columnIndex, int visualRows)
    {
        TableCell cell = base.BuildRowOperationCell(element, columnIndex, visualRows);
        List<TableCellAction> actions = cell.Actions.ToList();

        int index = ArrayNodeImpl.IndexOf(element);
        if (index > 0 && CanMutateRows)
        {
            actions.Add(new TableCellAction(
                LocaleManager.Instance.Get("avalonia.appfieldlist.button.moveup"),
                new RelayCommand(_ => MoveUp(element), _ => CanMutateRows)));
        }

        if (CanEditChildren(element))
        {
            actions.Add(new TableCellAction(
                LocaleManager.Instance.Get("avalonia.type.button.enter"),
                new RelayCommand(_ => OpenChildren(element), _ => !IsDisposed)));
        }

        return new TableCell(columnIndex, 0, visualRows, TableCellKind.RowOperation, null, actions);
    }

    private void MoveUp(DataNode element)
    {
        int index = ArrayNodeImpl.IndexOf(element);
        if (index <= 0) return;
        ArrayNodeImpl.MoveRow(index, index - 1);
    }

    private bool CanEditChildren(DataNode element)
    {
        if (IsDisposed || !CanMutateRows || EnumName() is null) return false;

        DataNode? value = element.GetAccessValue("value") as DataNode;
        if (value is null || value.GetProperty<ReadOnly>()?.Value != true)
            return false;

        IValueAccess? cascade = EnumDefinition()?.GetAccessValue("cascade");
        return cascade is ArrayNode array && array.Count > 1;
    }

    private void OpenChildren(DataNode element)
    {
        string? enumName = EnumName();
        string? parent = element.GetAccessValue("value")?.GetValue<string>();
        if (enumName is null || string.IsNullOrWhiteSpace(parent)) return;
        if (NodeImpl.Context is not SchemaContext context) return;

        _handle?.ViewModel.Dispose();
        EnumValuesEditorViewModelHandle handle = new();
        handle.ViewModel.Attach(context, enumName, parent);
        _handle = handle;

        EnumSubListWindow window = new() { DataContext = handle.ViewModel };
        window.Closed += (_, _) =>
        {
            handle.ViewModel.Dispose();
            if (ReferenceEquals(_handle, handle)) _handle = null;
        };

        if (OwnerWindow is { } owner)
        {
            _ = window.ShowDialog(owner);
            return;
        }

        window.Show();
    }

    private DataNode? EnumDefinition() => NodeImpl.Parent as DataNode;

    private string? EnumName()
    {
        DataNode? define = EnumDefinition();
        if (define is null) return null;

        string? space = define.GetAccessValue("namespace")?.GetValue<string>();
        string? name = define.GetAccessValue("name")?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(name) && define.Parent is DataNode { } owner)
        {
            space ??= owner.GetAccessValue("namespace")?.GetValue<string>();
            name = owner.GetAccessValue("name")?.GetValue<string>();
        }

        if (string.IsNullOrWhiteSpace(name)) return null;
        return string.IsNullOrWhiteSpace(space) ? name : $"{space}.{name}";
    }

    /// <summary>Keeps the editor lifetime tied to the window without reaching into its bindings.</summary>
    private sealed class EnumValuesEditorViewModelHandle
    {
        public ViewModels.EnumValuesEditorViewModel ViewModel { get; } = new();
    }
}
