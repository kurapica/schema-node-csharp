using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Runtime;

namespace SchemaNode.Property.Convert;

/// <summary>
/// The suffix text to append during emit
/// </summary>
[SchemaProperty([SchemaType.RecognizerPart], name: nameof(PrefixProperty))]
public class SuffixTextProperty : SchemaProperty<string>, IConvertProperty, ISuffixProperty
{
    /// <inheritdoc/>
    public string? Parse(SchemaContext context, string value, string result, AnySchemaNode? overrideValue = null)
    {
        string raw = result ?? value;
        return !string.IsNullOrWhiteSpace(Value) && raw.EndsWith(Value, StringComparison.OrdinalIgnoreCase) 
            ? raw.Substring(0, raw.Length - Value.Length) 
            : raw;
    }

    /// <inheritdoc/>
    public string? Emit(SchemaContext context, string value, string? result, AnySchemaNode? overrideValue = null)
    {
        string raw = result ?? value;
        return $"{raw}{Value}";
    }

    /// <inheritdoc/>
    public string? Suffix(string? name = null, AnySchemaType? type = null)
    {
        return Value;
    }
}
