using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Runtime;

namespace SchemaNode.Property;

/// <summary>
/// Declare a prefix property for property schema
/// </summary>
[SchemaProperty([SchemaType.Property])]
public sealed class PrefixProperty : SchemaProperty<long>;

/// <summary>
/// Mark a property as prefix information, which indicates that the property value is a prefix string used for recognizer validation.
/// </summary>
[SchemaPropertyKind(nameof(PrefixProperty), true)]
public interface IPrefixProperty : IProperty
{
    /// <summary>
    /// Gets the prefix value for the given name and type
    /// </summary>
    string? Prefix(string? name = null, AnySchemaType? type = null);
}
