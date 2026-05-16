using SchemaNode.Context;
using SchemaNode.Attribute;
using SchemaNode.Node;
using SchemaNode.Property.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Constraint;

[Meta<ForSchema>(SCHEMA_KIND_STRUCT_FIELD)]
public class WhiteList : Property<ArrayNode>, IConstraintProperty
{
    public bool? ValidateScalar(SchemaContext context, ScalarNode node, StructNode? parent = null, Node.IDataNode? overrideValue = null)
    {
        var list = overrideValue as ArrayNode ?? Value;
        if (list == null || node.IsEmpty) return null;
        return list.Any(v => v.Equals(node));
    }

    public bool? ValidateEnum(SchemaContext context, EnumNode node, StructNode? parent = null, Node.IDataNode? overrideValue = null)
    {
        var list = overrideValue as ArrayNode ?? Value;
        if (list == null || node.IsEmpty) return null;
        return list.Any(v => v.Equals(node));
    }
}