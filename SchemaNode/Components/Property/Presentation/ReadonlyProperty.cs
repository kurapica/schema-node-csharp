using SchemaNode.Attribute;
using SchemaNode.Enum;

namespace SchemaNode.Components.Property.Presentation;

/// <summary>
/// The node data is readonly.
/// </summary>
[SchemaProperty([SchemaType.StructField], [ValueSchemaType.All])]
public class ReadonlyProperty : SchemaProperty<bool>;
