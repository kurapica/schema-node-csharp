using System;
using System.Globalization;
using System.Windows.Data;

namespace SchemaNode.WPF.View.Localization;

/// <summary>
/// Resolves a locale key through <see cref="LocaleManager"/>.
///
/// Usage in XAML (direct binding, so every text property refreshes in place when the locale changes):
/// <c>{Binding Source={x:Static loc:LocaleManager.Instance}, Path=Version,
///   Converter={x:Static loc:LocalizedTextConverter.Instance}, ConverterParameter=wpf.title}</c>
/// </summary>
public sealed class LocalizedTextConverter : IValueConverter
{
    /// <summary>Singleton used by XAML <c>{x:Static}</c> references</summary>
    public static readonly LocalizedTextConverter Instance = new LocalizedTextConverter();

    private LocalizedTextConverter()
    {
    }

    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => parameter is string key ? LocaleManager.Instance.Get(key) : value?.ToString() ?? string.Empty;

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
