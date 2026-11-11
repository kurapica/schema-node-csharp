using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SchemaNode.Avalonia.View.NodeWrapper;

namespace SchemaNode.Avalonia.View.Views;

/// <summary>Text editor with the entry modes (drop-down, suggestion, cascade, multi-value).</summary>
public partial class InputView : UserControl
{
    /// <summary>
    /// Minimum width forwarded to the cascade path field. Cascade hosts that measure to content
    /// need the bound on the field itself; a bound on this control does not reach it.
    /// </summary>
    public static readonly StyledProperty<double> CascaderFieldMinWidthProperty =
        AvaloniaProperty.Register<InputView, double>(nameof(CascaderFieldMinWidth), 0d);

    public InputView()
    {
        InitializeComponent();
        ApplyCascaderFieldMinWidth();
    }

    /// <summary>Minimum width forwarded to the cascade path field; 0 keeps it content-sized.</summary>
    public double CascaderFieldMinWidth
    {
        get => GetValue(CascaderFieldMinWidthProperty);
        set => SetValue(CascaderFieldMinWidthProperty, value);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CascaderFieldMinWidthProperty)
            ApplyCascaderFieldMinWidth();
    }

    private void ApplyCascaderFieldMinWidth()
    {
        if (Cascader is not null)
            Cascader.FieldMinWidth = CascaderFieldMinWidth;
    }

    private void OnSuggestionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: EntryItem item }) return;
        if (DataContext is not InputWrapper wrapper) return;

        wrapper.SelectedEntry = item;
        wrapper.IsSuggestOpen = false;
    }

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        (DataContext as InputWrapper)?.ClearSelection();
    }
}
