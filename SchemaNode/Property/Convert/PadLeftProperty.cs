using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;

namespace SchemaNode.Property.Convert;

/// <summary>
/// Whether to pad on the left (true) or the right (false).
/// This property modifies the behavior of MinDigits and PadChar properties.
/// </summary>
[SchemaProperty([SchemaType.RecognizerPart])]
public class PadLeftProperty : SchemaProperty<bool>, IConvertProperty
{
    /// <inheritdoc/>
    public string? Emit(SchemaContext context, string value, string? result, AnySchemaNode? overrideValue = null) => result ?? value;
}
