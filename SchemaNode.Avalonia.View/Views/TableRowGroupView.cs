using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using SchemaNode.Avalonia.View.NodeWrapper;

namespace SchemaNode.Avalonia.View.Views;

/// <summary>
/// Renders one element of a table as a block of visual rows.
///
/// Every cell is absolutely placed with row/column spans, which is what produces rowspan merges:
/// sibling value cells span the whole element block while array-group cells occupy one visual row
/// each. Widths come from the table's shared leaf-column distribution, so the two header rows and
/// every body row always line up.
/// </summary>
public class TableRowGroupView : Grid
{
    private const double RowHeight = 28;
    private const double CellPadding = 4;

    private static readonly IBrush NormalBorderBrush = new SolidColorBrush(Color.Parse("#FFD0D0D0"));
    private static readonly IBrush ErrorBorderBrush = new SolidColorBrush(Color.Parse("#FFC62828"));

    private TableRowGroup? _group;
    private TableView? _owner;
    private int _builtLayoutVersion = -1;
    private readonly List<CellErrorBinding> _cellErrorBindings = [];

    public TableRowGroupView()
    {
        this.GetObservable(DataContextProperty).Subscribe(_ => Rebuild());
        HorizontalAlignment = HorizontalAlignment.Left;
        ClipToBounds = true;
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _owner = this.FindAncestorOfType<TableView>();
        if (_owner is not null)
            _owner.AvailableWidthChanged += OnOwnerWidthChanged;

        // Rebuild may return early for a container that was already built off-tree; the shared
        // widths must still be applied, otherwise a recycled row keeps the natural widths while
        // the header renders the distributed ones.
        Rebuild();
        ApplyWidths();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (_owner is not null)
            _owner.AvailableWidthChanged -= OnOwnerWidthChanged;
        _owner = null;
        ClearCellErrorBindings();
    }

    private void OnOwnerWidthChanged(object? sender, EventArgs e) => ApplyWidths();

    private void Rebuild()
    {
        if (DataContext is not TableRowGroup group)
        {
            _group = null;
            _builtLayoutVersion = -1;
            ClearCellErrorBindings();
            Children.Clear();
            ColumnDefinitions.Clear();
            RowDefinitions.Clear();
            return;
        }

        if (ReferenceEquals(group, _group) && _builtLayoutVersion == group.LayoutVersion)
            return;

        _group = group;
        _builtLayoutVersion = group.LayoutVersion;

        ClearCellErrorBindings();
        Children.Clear();
        ColumnDefinitions.Clear();
        RowDefinitions.Clear();

        for (int i = 0; i < group.ColumnWidths.Count; i++)
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Pixel) });
        for (int i = 0; i < group.VisualRowCount; i++)
            RowDefinitions.Add(new RowDefinition { Height = new GridLength(RowHeight, GridUnitType.Pixel) });

        Background = group.IsDeleted
            ? new SolidColorBrush(Color.Parse("#FFF0F0F0"))
            : null;

        foreach (TableCell cell in group.Cells)
        {
            Control content = BuildCellContent(cell, group);
            content.IsEnabled = !group.IsDeleted;

            Border border = new()
            {
                Child = content,
                Padding = new Thickness(CellPadding, 2),
                BorderBrush = NormalBorderBrush,
                BorderThickness = new Thickness(0, 0, 1, 1),
            };

            if (cell.Kind == TableCellKind.Value && cell.Wrapper is not null)
                BindCellError(border, cell.Wrapper);

            Grid.SetColumn(border, cell.ColumnIndex);
            Grid.SetRow(border, cell.Row);
            Grid.SetRowSpan(border, Math.Max(cell.RowSpan, 1));
            Children.Add(border);
        }

        ApplyWidths();
    }

    private Control BuildCellContent(TableCell cell, TableRowGroup group)
    {
        switch (cell.Kind)
        {
            case TableCellKind.Value:
                if (cell.Wrapper is null)
                    return new Panel();

                SchemaView view = new()
                {
                    Node = cell.Wrapper,
                    Role = SchemaViewRole.Cell,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                if (group.CellSkin is not null)
                    view.Skin = group.CellSkin;
                return view;

            case TableCellKind.SubOperation:
            case TableCellKind.RowOperation:
                StackPanel actions = new()
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                foreach (TableCellAction action in cell.Actions)
                {
                    actions.Children.Add(new Button
                    {
                        Content = action.Label,
                        Command = action.Command,
                        Background = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        Padding = new Thickness(0),
                        FontSize = 12,
                        VerticalAlignment = VerticalAlignment.Center,
                    });
                }
                return actions;

            default:
                return new Panel();
        }
    }

    /// <summary>
    /// Cell roles hide the inline error block, so a failing cell still needs a visible signal:
    /// a red outline plus the localized error text as the cell tooltip.
    /// </summary>
    private void BindCellError(Border border, DataNodeWrapper wrapper)
    {
        void Update()
        {
            string? error = wrapper.Error;
            border.BorderBrush = error is null ? NormalBorderBrush : ErrorBorderBrush;
            border.BorderThickness = error is null
                ? new Thickness(0, 0, 1, 1)
                : new Thickness(1);
            ToolTip.SetTip(border, error);
        }

        PropertyChangedEventHandler handler = (_, args) =>
        {
            if (args.PropertyName is null
                or nameof(DataNodeWrapper.Error)
                or nameof(DataNodeWrapper.HasErrors)
                or nameof(DataNodeWrapper.Value))
                Update();
        };

        wrapper.PropertyChanged += handler;
        _cellErrorBindings.Add(new CellErrorBinding(wrapper, handler));
        Update();
    }

    private void ClearCellErrorBindings()
    {
        foreach (CellErrorBinding binding in _cellErrorBindings)
            binding.Wrapper.PropertyChanged -= binding.Handler;
        _cellErrorBindings.Clear();
    }

    private readonly record struct CellErrorBinding(
        DataNodeWrapper Wrapper,
        PropertyChangedEventHandler Handler);

    /// <summary>Applies the shared leaf widths in place (no child recreation).</summary>
    private void ApplyWidths()
    {
        if (_group is null || ColumnDefinitions.Count == 0) return;

        // The table distributes once; every row consumes the same snapshot as the header. Only a
        // row hosted without a table falls back to its own natural widths.
        IReadOnlyList<double>? shared = _owner?.EffectiveColumnWidths;
        double[] widths = shared is not null && shared.Count == ColumnDefinitions.Count
            ? shared.ToArray()
            : TableColumnLayout.Distribute(
                _group.ColumnWidths, _group.MinColumnWidths, _owner?.EffectiveAvailableWidth ?? 0);

        for (int i = 0; i < widths.Length && i < ColumnDefinitions.Count; i++)
        {
            ColumnDefinition column = ColumnDefinitions[i];
            if (Math.Abs(column.Width.Value - widths[i]) < 0.5) continue;
            column.Width = new GridLength(widths[i], GridUnitType.Pixel);
        }

        double total = widths.Sum();
        if (Math.Abs(Width - total) > 0.5)
            Width = total;
    }
}
