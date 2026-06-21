using SchemaNode.Enum;
using SchemaNode.Attribute;
using SchemaNode.Node;

namespace SchemaNode.Property.Common;

[SchemaProperty([SchemaType.Scalar, SchemaType.Enum, SchemaType.StructField],
    [ValueSchemaType.Bool, ValueSchemaType.String, ValueSchemaType.Number, ValueSchemaType.Date, ValueSchemaType.Enum],
    includeArray: true)]
public class DefaultProperty : SchemaProperty<AnySchemaNode> { }