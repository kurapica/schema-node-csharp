using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using System.Globalization;

namespace SchemaNode.Property.Convert;

/// <summary>
/// Number of decimal places for floating-point scalar types.
/// Example: Precision = 2, value 3.1 → "3.10"
/// </summary>
[SchemaProperty([SchemaType.RecognizerPart])]
public class PrecisionProperty : SchemaProperty<int>, IConvertProperty
{
    /// <inheritdoc/>
    public string? Emit(SchemaContext context, string value, string? result, AnySchemaNode? overrideValue = null)
    {
        string raw = result ?? value;
        if (Value is not > 0) return raw;

        if (decimal.TryParse(raw, CultureInfo.InvariantCulture, out var d))
            return d.ToString($"F{Value}", CultureInfo.InvariantCulture);

        return raw;
    }
}
