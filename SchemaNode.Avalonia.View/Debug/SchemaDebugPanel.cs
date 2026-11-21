using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace SchemaNode.Avalonia.View.Debug;

/// <summary>
/// Inspector body. It is a plain control bound to a <see cref="DebugNodeSnapshot"/>; the button
/// that opens it exists only while the inheritable <c>SchemaView.Debug</c> property is on, so the
/// type is compiled in every configuration but never materialized unless a host asks for it.
/// </summary>
public sealed class SchemaDebugPanel : Border
{
    // The panel sizes to its content: names, values and access paths keep their full text and the
    // body scrolls when they do not fit, instead of ellipsing them. The cap keeps a very long value
    // from stretching the popup across the screen.
    private const double MinPanelWidth = 320;
    private const double MaxPanelWidth = 720;
    private const double MaxBodyHeight = 420;

    /// <summary>Creates the panel.</summary>
    public SchemaDebugPanel()
    {
        Background = new SolidColorBrush(Color.Parse("#FFFFFDF2"));
        BorderBrush = new SolidColorBrush(Color.Parse("#FFC9A227"));
        BorderThickness = new Thickness(1);
        Padding = new Thickness(8);
        MinWidth = MinPanelWidth;
        MaxWidth = MaxPanelWidth;
    }

    /// <summary>Shows the snapshot for a node.</summary>
    public void Show(DebugNodeSnapshot snapshot)
    {
        Child = Build(snapshot);
    }

    private static Control Build(DebugNodeSnapshot snapshot)
    {
        StackPanel content = new() { Spacing = 2 };
        content.Children.Add(new TextBlock
        {
            Text = snapshot.Header,
            FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap,
        });

        content.Children.Add(SectionTitle("Properties"));
        content.Children.Add(Section(
            snapshot.Properties.Select(row => new[] { row.Name, row.Value, row.Source }).ToList(),
            [110, 150, 0]));

        content.Children.Add(SectionTitle("Relations"));
        content.Children.Add(Section(
            snapshot.Relations.Select(row => new[] { row.Property, row.Mode, row.Data, row.Source }).ToList(),
            [110, 90, 80, 0]));

        content.Children.Add(SectionTitle("Errors"));
        content.Children.Add(snapshot.HasErrors
            ? Section(
                snapshot.Errors.Select(row => new[] { row.Access, row.Constraint, row.Message }).ToList(),
                [150, 90, 0])
            : new TextBlock { Text = snapshot.NoErrorsText, Foreground = Brushes.Gray });

        return new ScrollViewer
        {
            MaxHeight = MaxBodyHeight,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content,
        };
    }

    private static TextBlock SectionTitle(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 8, 0, 2),
        };
    }

    /// <summary>
    /// Builds one section as a single grid, so auto-sized columns are shared by every row and all
    /// cells stay aligned while still showing their complete text.
    /// </summary>
    private static Control Section(IReadOnlyList<string[]> rows, double[] minWidths)
    {
        Grid grid = new();

        for (int column = 0; column < minWidths.Length; column++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MinWidth = minWidths[column] });

        for (int row = 0; row < rows.Count; row++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            string[] cells = rows[row];
            for (int column = 0; column < cells.Length; column++)
            {
                bool last = column == cells.Length - 1;
                TextBlock cell = new()
                {
                    Text = cells[column],
                    Margin = new Thickness(0, 1, last ? 0 : 8, 1),
                };
                // Only the trailing source column is secondary; leaving the primary cells untouched
                // keeps their inherited theme foreground (assigning null would render them invisible).
                if (last) cell.Foreground = Brushes.Gray;
                Grid.SetRow(cell, row);
                Grid.SetColumn(cell, column);
                grid.Children.Add(cell);
            }
        }

        return grid;
    }
}
