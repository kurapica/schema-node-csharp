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
    private static readonly Brush CellErrorBorder = new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));
    private static readonly Brush DeletedRowBackground = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0));

    /// <summary>
    /// Horizontal space the hosting table has for its columns. The row applies the same
    /// <see cref="TableColumnLayout"/> distribution as the header rows: natural widths grow to fill
    /// the host, shrink down to the column minimum, and only then overflow into the body scroll.
    /// </summary>
    public static readonly DependencyProperty AvailableWidthProperty = DependencyProperty.Register(
        nameof(AvailableWidth),
        typeof(double),
        typeof(TableRowGroupView),
        new PropertyMetadata(0d, OnAvailableWidthChanged));

    public TableRowGroupView()
    {
        HorizontalAlignment = HorizontalAlignment.Left;
        DataContextChanged += (_, _) => Build();
    }

    /// <summary>Horizontal space available to the table's columns; zero means natural width.</summary>
    public double AvailableWidth
    {
        get => (double)GetValue(AvailableWidthProperty);
        set => SetValue(AvailableWidthProperty, value);
    }

    private static void OnAvailableWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((TableRowGroupView)d).ApplyColumnWidths();

    private void Build()
    {
        Children.Clear();
        ColumnDefinitions.Clear();
        RowDefinitions.Clear();

        if (DataContext is not TableRowGroup group) return;

        Background = group.IsDeleted ? DeletedRowBackground : Brushes.Transparent;

        foreach (double _ in group.ColumnWidths)
            ColumnDefinitions.Add(new ColumnDefinition());

        ApplyColumnWidths();

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

    /// <summary>
    /// Applies the column widths for the current <see cref="AvailableWidth"/> without rebuilding
    /// the cells, so a host resize never replaces the content subtree.
    /// </summary>
    private void ApplyColumnWidths()
    {
        if (DataContext is not TableRowGroup group) return;
        if (ColumnDefinitions.Count != group.ColumnWidths.Count) return;

        double[] widths = TableColumnLayout.Distribute(group.ColumnWidths, group.MinColumnWidths, AvailableWidth);

        for (int i = 0; i < ColumnDefinitions.Count; i++)
        {
            double width = widths[i];
            ColumnDefinition definition = ColumnDefinitions[i];
            // Assigning a GridLength invalidates layout; skip unchanged widths so a resize loop
            // cannot keep the table in a permanent layout pass.
            if (definition.Width.IsAbsolute && Math.Abs(definition.Width.Value - width) < 0.5) continue;
            definition.Width = new GridLength(width);
        }
    }

    /// <summary>
    /// Cell border that turns red when the leaf it hosts reports an error. Containers keep
    /// <c>ShowCellError</c> false, so a struct or array cell never repeats a child's failure.
    /// </summary>
    private static Style CreateValueCellStyle(DataNodeWrapper? wrapper)
    {
        Style style = new() { TargetType = typeof(Border) };
        style.Setters.Add(new Setter(Border.BorderBrushProperty, CellBorder));

        if (wrapper is null) return style;

        DataTrigger failed = new()
        {
            Binding = new Binding(nameof(DataNodeWrapper.ShowCellError)) { Source = wrapper },
            Value = true,
        };
        failed.Setters.Add(new Setter(Border.BorderBrushProperty, CellErrorBorder));
        style.Triggers.Add(failed);
        return style;
    }

    private static FrameworkElement CreateCell(TableCell cell, string? cellSkin)
    {
        if (cell.Kind == TableCellKind.Value)
        {
            Border value = new()
            {
                BorderThickness = new Thickness(0, 0, 1, 1),
                Padding = new Thickness(6, 0, 6, 0),
                Style = CreateValueCellStyle(cell.Wrapper),
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

                // Compact cells have no room for an inline error block; the tooltip carries the
                // message. Only an editable cell can be fixed there, so a read-only or derived
                // column must not advertise a failure the user cannot act on.
                if (cell.Wrapper.IsEditable)
                {
                    value.SetBinding(FrameworkElement.ToolTipProperty, new Binding(nameof(DataNodeWrapper.Error))
                    {
                        Source = cell.Wrapper,
                        Mode = BindingMode.OneWay,
                    });
                }

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
