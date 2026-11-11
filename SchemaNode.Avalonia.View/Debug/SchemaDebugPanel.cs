using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SchemaNode.Avalonia.View.Debug;

/// <summary>
/// Debug-only inspector body. It is a plain control bound to a <see cref="DebugNodeSnapshot"/>;
/// the button that opens it is created only in DEBUG builds. Built in code so the Release build
/// can exclude the whole type without dragging an XAML asset along.
/// </summary>
public sealed class SchemaDebugPanel : Border
{
    /// <summary>Creates the panel.</summary>
    public SchemaDebugPanel()
    {
        Background = new SolidColorBrush(Color.Parse("#FFFFFDF2"));
        BorderBrush = new SolidColorBrush(Color.Parse("#FFC9A227"));
        BorderThickness = new Thickness(1);
        Padding = new Thickness(8);
        MaxWidth = 560;
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

        content.Children.Add(new TextBlock
        {
            Text = "Properties",
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 8, 0, 2),
        });
        foreach (DebugPropertyRow row in snapshot.Properties)
            content.Children.Add(Row("110,150,*", row.Name, row.Value, row.Source));

        content.Children.Add(new TextBlock
        {
            Text = "Relations",
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 8, 0, 2),
        });
        foreach (DebugRelationRow row in snapshot.Relations)
            content.Children.Add(Row("110,90,80,*", row.Property, row.Mode, row.Data, row.Source));

        return new ScrollViewer
        {
            MaxHeight = 420,
            Content = content,
        };
    }

    private static Control Row(string columns, params string[] cells)
    {
        Grid grid = new()
        {
            ColumnDefinitions = new ColumnDefinitions(columns),
            Margin = new Thickness(0, 1),
        };

        for (int i = 0; i < cells.Length; i++)
        {
            TextBlock cell = new()
            {
                Text = cells[i],
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            // Only the trailing source column is secondary; leaving the primary cells untouched keeps
            // their inherited theme foreground (assigning null would render them invisible).
            if (i == cells.Length - 1) cell.Foreground = Brushes.Gray;
            Grid.SetColumn(cell, i);
            grid.Children.Add(cell);
        }

        return grid;
    }
}
