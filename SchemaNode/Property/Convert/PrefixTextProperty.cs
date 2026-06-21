using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Runtime;

namespace SchemaNode.Property.Convert;

/// <summary>
/// The prefix text to prepend during emit
/// </summary>
[SchemaProperty([SchemaType.RecognizerPart], name: nameof(PrefixProperty))]
public class PrefixTextProperty : SchemaProperty<string>, IConvertProperty, IPrefixProperty
{
    /// <inheritdoc/>
    public string? Parse(SchemaContext context, string value, string result, AnySchemaNode? overrideValue = null)
    {
        string raw = result ?? value;
        return !string.IsNullOrWhiteSpace(Value) && raw.StartsWith(Value, StringComparison.OrdinalIgnoreCase)
            ? raw.Substring(Value.Length)
            : raw;
    }

    /// <inheritdoc/>
    public string? Emit(SchemaContext context, string value, string? result, AnySchemaNode? overrideValue = null)
    {
        string raw = result ?? value;
        return $"{Value}{raw}";
    }

    /// <inheritdoc/>
    public string? Prefix(string? name = null, AnySchemaType? type = null)
    {
        return Value;
    }
}
