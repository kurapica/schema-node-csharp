using SchemaNode.Utility;

namespace SchemaNode.Attribute;

[AttributeUsage(AttributeTargets.Interface)]
public class SchemaPropertyKindAttribute(string kind) : System.Attribute
{
    /// <summary>
    /// The property kind, such as "presentation", "constraint", etc.
    /// </summary>
    public string Kind { get; } = (kind.EndsWith("Property") ? kind[..^"Property".Length] : kind).ToCamelCase();
}
