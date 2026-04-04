using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Runtime;

namespace SchemaNode.Property;

/// <summary>
/// Declare a suffix property for property schema
/// </summary>
[SchemaProperty([SchemaType.Property])]
public sealed class SuffixProperty : SchemaProperty<long>;

/// <summary>
/// Mark a property as suffix information, which indicates that the property value is a suffix string used for recognizer validation.
/// </summary>
[SchemaPropertyKind(nameof(SuffixProperty), true)]
public interface ISuffixProperty : IProperty
{
    /// <summary>
    /// Gets the suffix value for the given name and type
    /// </summary>
    string? Suffix(string? name = null, AnySchemaType? type = null);
}
