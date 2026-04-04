using SchemaNode.Attribute;
using SchemaNode.Enum;

namespace SchemaNode.Property.Presentation;

/// <summary>
/// When calculating the up limit, add the original value.
/// </summary>
[SchemaProperty([SchemaType.StructField], [ValueSchemaType.Number])]
public class StackUpLimitProperty : SchemaProperty<bool>;
