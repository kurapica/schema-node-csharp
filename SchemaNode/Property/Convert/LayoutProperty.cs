using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using System.Globalization;

namespace SchemaNode.Property.Convert;

/// <summary>
/// Date/time layout string (e.g., "yyyy-MM-dd", "HH:mm:ss").
/// Applied when the scalar base type is a date or datetime.
/// During parse: parses the input string using the layout and returns ISO 8601 format.
/// During emit: formats the ISO 8601 string using the layout.
/// </summary>
[SchemaProperty([SchemaType.RecognizerPart])]
public class LayoutProperty : SchemaProperty<string>, IConvertProperty
{
    /// <inheritdoc/>
    public string? Parse(SchemaContext context, string value, string result, AnySchemaNode? overrideValue = null)
    {
        string raw = result ?? value;
        if (string.IsNullOrWhiteSpace(Value)) return raw;

        if (DateTimeOffset.TryParseExact(raw, Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto))
            return dto.ToString("O");

        return null;
    }

    /// <inheritdoc/>
    public string? Emit(SchemaContext context, string value, string? result, AnySchemaNode? overrideValue = null)
    {
        string raw = result ?? value;
        if (string.IsNullOrWhiteSpace(Value)) return raw;

        if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto))
            return dto.ToString(Value, CultureInfo.InvariantCulture);

        return raw;
    }
}
