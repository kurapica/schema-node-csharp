using SchemaNode.Attribute;
using SchemaNode.Enum;

namespace SchemaNode.Property.Common;

/// <summary>
/// The node should be visible.
/// </summary>
[SchemaProperty([SchemaType.StructField], [ValueSchemaType.All])]
public class VisibleProperty : SchemaProperty<bool>, IRelationOnlyProperty;
