using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using SchemaNode.WPF.View.NodeWrapper;

namespace SchemaNode.WPF.View.Views;

/// <summary>
/// Lays out one element block as a grid whose row count is the number of visual rows the element
/// occupies. Non-array cells span all visual rows (rowspan), array cells occupy one row each, which
/// reproduces vue's merged sibling cells for a single element.
/// </summary>
public sealed class TableRowGroupView : Grid
{
    private const double RowHeight = 30;
    private static readonly Brush CellBorder = new SolidColorBrush(Color.FromRgb(0xD9, 0xD9, 0xD9));
    private static readonly Brush DeletedRowBackground = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0));

    public TableRowGroupView()
    {
        HorizontalAlignment = HorizontalAlignment.Left;
        DataContextChanged += (_, _) => Build();
    }

    private void Build()
    {
        Children.Clear();
        ColumnDefinitions.Clear();
        RowDefinitions.Clear();

        if (DataContext is not TableRowGroup group) return;

        Background = group.IsDeleted ? DeletedRowBackground : Brushes.Transparent;

        foreach (double width in group.ColumnWidths)
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });

        for (int row = 0; row < group.VisualRowCount; row++)
            RowDefinitions.Add(new RowDefinition { Height = new GridLength(RowHeight) });

        foreach (TableCell cell in group.Cells)
        {
            FrameworkElement element = CreateCell(cell, group.CellSkin);
            SetRow(element, cell.Row);
            SetRowSpan(element, cell.RowSpan);
            SetColumn(element, cell.ColumnIndex);
            Children.Add(element);
        }
    }

    private static FrameworkElement CreateCell(TableCell cell, string? cellSkin)
    {
        if (cell.Kind == TableCellKind.Value)
        {
            Border value = new()
            {
                BorderBrush = CellBorder,
                BorderThickness = new Thickness(0, 0, 1, 1),
                Padding = new Thickness(6, 0, 6, 0),
            };

            if (cell.Wrapper is not null)
            {
                // A relation can hide the cell's node (or a structured parent it belongs to).
                // Collapse the cell rather than rebuilding rows or columns, so an inherited
                // visibility change never replaces the content subtree.
                value.SetBinding(UIElement.VisibilityProperty, new Binding(nameof(DataNodeWrapper.IsVisible))
                {
                    Source = cell.Wrapper,
                    Mode = BindingMode.OneWay,
                    Converter = Converters.BoolToVisibility,
                });

                value.ToolTip = cell.Wrapper.Error;
                SchemaView view = new()
                {
                    Node = cell.Wrapper,
                    Role = SchemaViewRole.Cell,
                    VerticalAlignment = VerticalAlignment.Center,
                };

                // A cell skin set on the table overrides the inherited one; null keeps inheriting
                // from the table view, so nothing changes when no cell skin was configured.
                if (cellSkin is not null) view.Skin = cellSkin;
                value.Child = view;
            }

            return value;
        }

        StackPanel actions = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        foreach (TableCellAction action in cell.Actions)
        {
            actions.Children.Add(new Button
            {
                Content = action.Label,
                Command = action.Command,
                Padding = new Thickness(4, 0, 4, 0),
                MinHeight = 20,
                FontSize = 11,
                Margin = new Thickness(2, 0, 2, 0),
            });
        }

        return new Border
        {
            Child = actions,
            BorderBrush = CellBorder,
            BorderThickness = new Thickness(0, 0, 1, 1),
        };
    }
}
