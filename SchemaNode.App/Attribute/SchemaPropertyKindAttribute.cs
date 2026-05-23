using SchemaNode.Utility;

namespace SchemaNode.Attribute;

[AttributeUsage(AttributeTargets.Interface)]
public class SchemaPropertyKindAttribute(string kind, bool mutuallyExclusive = false) : System.Attribute
{
    /// <summary>
    /// The property kind, such as "presentation", "constraint", etc.
    /// </summary>
    public string Kind { get; } = (kind.EndsWith("Property", StringComparison.OrdinalIgnoreCase) ? kind[..^"Property".Length] : kind.EndsWith("Prop", StringComparison.OrdinalIgnoreCase) ? kind[..^"Prop".Length] : kind).ToCamelCase();

    /// <summary>
    /// The mutually exclusive flag indicates whether properties of this kind are mutually exclusive on the same schema node.
    /// </summary>
    public bool MutuallyExclusive { get; set; } = mutuallyExclusive;
}
