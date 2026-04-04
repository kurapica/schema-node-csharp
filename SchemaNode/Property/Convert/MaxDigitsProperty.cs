using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;

namespace SchemaNode.Property.Convert;

/// <summary>
/// Maximum number of digits (truncated from the left if longer).
/// Applies to integer or numeric scalar types.
/// </summary>
[SchemaProperty([SchemaType.RecognizerPart])]
public class MaxDigitsProperty : SchemaProperty<int>, IConvertProperty
{
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

        if (intPart.Length > Value)
            intPart = intPart[^Value..];

        return sign + intPart + decPart;
    }
}
