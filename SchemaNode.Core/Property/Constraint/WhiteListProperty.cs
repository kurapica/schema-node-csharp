using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Attribute;
using SchemaNode.Node;

namespace SchemaNode.Property.Constraint;

[SchemaProperty([SchemaType.StructField], [ValueSchemaType.Scalar, ValueSchemaType.Enum],
    includeArray: true, optionDepends: [nameof(RequireProperty)])]
public class WhiteListProperty : SchemaProperty<ArrayNode>, IConstraintProperty
{
    public bool? ValidateScalar(SchemaContext context, ScalarNode node, StructNode? parent = null, AnySchemaNode? overrideValue = null)
    {
        var list = overrideValue as ArrayNode ?? Value;
        if (list == null || node.IsEmpty) return null;
        return list.Any(v => v.Equals(node));
    }

    public bool? ValidateEnum(SchemaContext context, EnumNode node, StructNode? parent = null, AnySchemaNode? overrideValue = null)
    {
        var list = overrideValue as ArrayNode ?? Value;
        if (list == null || node.IsEmpty) return null;
        return list.Any(v => v.Equals(node));
    }
}