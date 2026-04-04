using SchemaNode.Attribute;
using SchemaNode.Enum;

namespace SchemaNode.Property.Presentation;

/// <summary>
/// The node should be invisible.
/// </summary>
[SchemaProperty([SchemaType.StructField], [ValueSchemaType.All])]
public class InvisibleProperty : SchemaProperty<bool>;
