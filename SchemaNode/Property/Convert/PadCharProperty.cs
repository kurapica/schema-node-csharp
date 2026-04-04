using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;

namespace SchemaNode.Property.Convert;

/// <summary>
/// The padding character. Used together with MinDigits or a fixed-width layout.
/// During parse, strips the pad character from the input.
/// </summary>
[SchemaProperty([SchemaType.RecognizerPart])]
public class PadCharProperty : SchemaProperty<string>, IConvertProperty
{
    /// <summary>
    /// Any non-null string is a valid pad character (including whitespace)
    /// </summary>
    public override bool HasValue => Value != null;

    /// <summary>
    /// The pad direction, wired from PadLeftProperty at runtime
    /// </summary>
    internal bool PadLeft { get; set; } = true;

    /// <inheritdoc/>
    public string? Parse(SchemaContext context, string value, string result, AnySchemaNode? overrideValue = null)
    {
        string raw = result ?? value;
        if (Value is not { Length: > 0 }) return raw;

        char padChar = Value[0];
        raw = PadLeft ? raw.TrimStart(padChar) : raw.TrimEnd(padChar);
        if (raw.Length == 0) raw = "0";

        return raw;
    }

    /// <inheritdoc/>
    public string? Emit(SchemaContext context, string value, string? result, AnySchemaNode? overrideValue = null) => result ?? value;
}
