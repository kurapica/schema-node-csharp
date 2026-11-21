using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SchemaNode.WPF.View;

/// <summary>Public bool → Visibility converter, usable directly from host XAML.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility.Visible;
}
