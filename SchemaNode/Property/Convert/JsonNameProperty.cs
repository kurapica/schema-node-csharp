using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Runtime;

namespace SchemaNode.Property.Convert;

/// <summary>
/// XML attribute prefix: name="value"
/// </summary>
[SchemaProperty([SchemaType.RecognizerPart])]
public sealed class JsonNameProperty : SchemaProperty<bool>, IConvertProperty, IPrefixProperty
{
    string? jsonName = null;

    public string? Parse(SchemaContext context, string value, string result, AnySchemaNode? overrideValue = null)
    {
        string raw = result ?? value;

        int idx = raw.IndexOf('=', StringComparison.Ordinal);
        if (idx < 0) return raw;

        return raw.Substring(idx + 1).Trim().Trim('"');
    }

    public string? Emit(SchemaContext context, string value, string? result, AnySchemaNode? overrideValue = null)
    {
        if (Value != true || jsonName == null) return result ?? value;

        string raw = result ?? value;
        return $"\"{jsonName}\": {raw}";
    }

    public string? Prefix(string? name = null, AnySchemaType? type = null)
    {
        if (Value != true) return null;
        jsonName = name;
        return name != null ? $"\"{jsonName}\":" : null;
    }
}
