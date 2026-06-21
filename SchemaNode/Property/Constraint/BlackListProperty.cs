using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Attribute;
using SchemaNode.Node;

namespace SchemaNode.Property.Constraint;

[SchemaProperty([SchemaType.StructField], [ValueSchemaType.Scalar, ValueSchemaType.Enum],
    includeArray: true, optionDepends: [nameof(RequireProperty)])]
public class BlackListProperty : SchemaProperty<ArrayTypeNode>, IConstraintProperty
{
    public bool? ValidateScalar(SchemaContext context, ScalarTypeNode node, StructTypeNode? parent = null, AnySchemaNode? overrideValue = null)
    {
        var list = overrideValue as ArrayTypeNode ?? Value;
        if (list == null || node.IsEmpty) return null;
        return list.All(v => !v.Equals(node));
    }

    public bool? ValidateEnum(SchemaContext context, EnumTypeNode node, StructTypeNode? parent = null, AnySchemaNode? overrideValue = null)
    {
        var list = overrideValue as ArrayTypeNode ?? Value;
        if (list == null || node.IsEmpty) return null;
        return list.All(v => !v.Equals(node));
    }
}