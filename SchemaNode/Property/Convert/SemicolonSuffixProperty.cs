using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Runtime;

namespace SchemaNode.Property.Convert;

/// <summary>
/// The suffix text to append during emit
/// </summary>
[SchemaProperty([SchemaType.RecognizerPart])]
public class SemicolonSuffixProperty : SchemaProperty<bool>, IConvertProperty, ISuffixProperty
{
    /// <inheritdoc/>
    public string? Parse(SchemaContext context, string value, string result, AnySchemaNode? overrideValue = null)
    {
        string raw = result ?? value;
        return raw.EndsWith(";") ? raw.Substring(0, raw.Length - 1) : raw;
    }

    /// <inheritdoc/>
    public string? Emit(SchemaContext context, string value, string? result, AnySchemaNode? overrideValue = null)
    {
        string raw = result ?? value;
        return $"{raw};";
    }

    /// <inheritdoc/>
    public string? Suffix(string? name = null, AnySchemaType? type = null)
    {
        return ";";
    }
}
