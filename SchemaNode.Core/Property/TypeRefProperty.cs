using SchemaNode.Attribute;
using SchemaNode.Enum;

namespace SchemaNode.Property;

/// <summary>
/// Mark a property as a type reference, which indicates that the property value is a reference to another type
/// </summary>
public interface ITypeRefProperty : IProperty;
