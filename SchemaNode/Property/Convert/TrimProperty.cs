using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;

namespace SchemaNode.Property.Convert;

/// <summary>
/// Whether to trim leading and trailing whitespace from the string value.
/// </summary>
[SchemaProperty([SchemaType.RecognizerPart])]
public class TrimProperty : SchemaProperty<bool>, IConvertProperty
{
    /// <inheritdoc/>
    public string? Parse(SchemaContext context, string value, string result, AnySchemaNode? overrideValue = null)
    {
        string raw = result ?? value;
        return Value == true ? raw.Trim() : raw;
    }

    /// <inheritdoc/>
    public string? Emit(SchemaContext context, string value, string? result, AnySchemaNode? overrideValue = null)
    {
        string raw = result ?? value;
        return Value == true ? raw.Trim() : raw;
    }
}
