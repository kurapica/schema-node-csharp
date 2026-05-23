using SchemaNode.Attribute;
using SchemaNode.Enum;

namespace SchemaNode.Property.Common;

/// <summary>
/// The node data is readonly.
/// </summary>
[SchemaProperty([SchemaType.StructField], [ValueSchemaType.All])]
public class ReadonlyProperty : SchemaProperty<bool>;
