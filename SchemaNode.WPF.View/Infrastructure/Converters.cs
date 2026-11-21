using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace SchemaNode.WPF.View;

/// <summary>
/// Stateless converters used by the built-in templates. They are exposed as static fields so
/// XAML can reference them through <c>{x:Static}</c> without a local ResourceDictionary.
/// </summary>
public static class Converters
{
    /// <summary>true → Visible, false → Collapsed</summary>
    public static readonly IValueConverter BoolToVisibility = new SchemaNode.WPF.View.BoolToVisibilityConverter();

    /// <summary>true → red border brush, false → transparent</summary>
    public static readonly IValueConverter ErrorToBrush = new ErrorToBrushConverter();

    /// <summary>true → Collapsed, false → Visible</summary>
    public static readonly IValueConverter InverseBoolToVisibility = new InverseBoolToVisibilityConverter();

    /// <summary>Boolean negation</summary>
    public static readonly IValueConverter Not = new NotConverter();

    /// <summary>Non-empty string → Visible, null/empty → Collapsed</summary>
    public static readonly IValueConverter VisibleIfText = new VisibleIfTextConverter();

    private sealed class VisibleIfTextConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is string text && !string.IsNullOrWhiteSpace(text) ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    private sealed class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is true ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is Visibility.Collapsed;
    }

    private sealed class NotConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is not true;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is not true;
    }

    private sealed class ErrorToBrushConverter : IValueConverter
    {
        private static readonly Brush ErrorBrush = new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));
        private static readonly Brush NormalBrush = Brushes.Transparent;

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is true ? ErrorBrush : NormalBrush;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
