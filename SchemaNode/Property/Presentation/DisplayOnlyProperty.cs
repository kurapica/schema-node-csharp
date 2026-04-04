using SchemaNode.Attribute;
using SchemaNode.Enum;

namespace SchemaNode.Property.Presentation;

/// <summary>
/// The node should be display only, won't be submit or saved.
/// </summary>
[SchemaProperty([SchemaType.StructField], [ValueSchemaType.All])]
public class DisplayOnlyProperty : SchemaProperty<bool>;
