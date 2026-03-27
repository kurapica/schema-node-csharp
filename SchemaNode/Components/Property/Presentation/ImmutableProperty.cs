using SchemaNode.Attribute;
using SchemaNode.Enum;

namespace SchemaNode.Components.Property.Presentation;

/// <summary>
/// The node data is immutable, un-changeable if init-ed.
/// </summary>
[SchemaProperty([SchemaType.StructField], [ValueSchemaType.All])]
public class ImmutableProperty : SchemaProperty<bool>;
