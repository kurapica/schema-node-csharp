using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;

namespace SchemaNode.Property.Convert;

/// <summary>
/// Minimum number of digits (zero-padded on the left if shorter).
/// Applies to integer or numeric scalar types.
/// Example: MinDigits = 3, value 7 → "007"
/// </summary>
[SchemaProperty([SchemaType.RecognizerPart], optionDepends: [nameof(PrecisionProperty), nameof(MaxDigitsProperty)])]
public class MinDigitsProperty : SchemaProperty<int>, IConvertProperty
{
    /// <summary>
    /// The pad character, wired from PadCharProperty at runtime
    /// </summary>
    internal char PadChar { get; set; } = '0';

    /// <summary>
    /// The pad direction, wired from PadLeftProperty at runtime
    /// </summary>
    internal bool PadLeft { get; set; } = true;

    /// <inheritdoc/>
    public string? Emit(SchemaContext context, string value, string? result, AnySchemaNode? overrideValue = null)
    {
        string raw = result ?? value;
        if (Value is not > 0) return raw;

        string sign = "";
        string body = raw;
        if (body.StartsWith('-'))
        {
            sign = "-";
            body = body[1..];
        }

        int dotIdx = body.IndexOf('.');
        string intPart = dotIdx >= 0 ? body[..dotIdx] : body;
        string decPart = dotIdx >= 0 ? body[dotIdx..] : "";

        if (intPart.Length < Value)
            intPart = PadLeft ? intPart.PadLeft(Value, PadChar) : intPart.PadRight(Value, PadChar);

        return sign + intPart + decPart;
    }
}
