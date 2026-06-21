using SchemaNode.Attribute;
using SchemaNode.Enum;

namespace SchemaNode.Property;

/// <summary>
/// Mark a property as a type reference, which indicates that the property value is a reference to another type
/// </summary>
[SchemaPropertyKind(nameof(ITypeRefProperty))]
public interface ITypeRefProperty : IProperty;

/// <summary>
/// Declare a constraint property for property schema
/// </summary>
[SchemaProperty([SchemaType.Property])]
public sealed class TypeRefProperty : SchemaProperty<bool>;