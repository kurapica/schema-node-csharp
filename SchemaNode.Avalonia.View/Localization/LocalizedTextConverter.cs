using System.Globalization;
using Avalonia.Data.Converters;

namespace SchemaNode.Avalonia.View.Localization;

/// <summary>
/// Binding converter that resolves a locale key (passed as the converter parameter) against the
/// current <see cref="LocaleManager"/>. Bind it to <see cref="LocaleManager.Version"/> so the text
/// refreshes in place when the language changes.
/// </summary>
public sealed class LocalizedTextConverter : IValueConverter
{
    /// <summary>Stateless singleton referenced from bindings.</summary>
    public static readonly LocalizedTextConverter Instance = new();

    private LocalizedTextConverter()
    {
    }

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => parameter is string key ? LocaleManager.Instance.Get(key) : value;

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
