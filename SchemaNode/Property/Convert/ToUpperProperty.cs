using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;

namespace SchemaNode.Property.Convert;

/// <summary>
/// Whether to convert the string value to upper case.
/// </summary>
[SchemaProperty([SchemaType.RecognizerPart])]
public class ToUpperProperty : SchemaProperty<bool>, IConvertProperty
{
    /// <inheritdoc/>
    public string? Parse(SchemaContext context, string value, string result, AnySchemaNode? overrideValue = null)
    {
        string raw = result ?? value;
        return Value == true ? raw.ToUpperInvariant() : raw;
    }

    /// <inheritdoc/>
    public string? Emit(SchemaContext context, string value, string? result, AnySchemaNode? overrideValue = null)
    {
        string raw = result ?? value;
        return Value == true ? raw.ToUpperInvariant() : raw;
    }
}
