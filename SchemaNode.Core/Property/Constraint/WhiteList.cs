using SchemaNode.Context;
using SchemaNode.Attribute;
using SchemaNode.Node;
using SchemaNode.Property.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Constraint;

[Meta<ForSchema>(SCHEMA_KIND_STRUCT_FIELD)]
public class WhiteList : Property<ArrayNode>, IConstraintProperty
{
    public virtual bool? Validate(SchemaContext context, DataNode node)
    {
        var list = Value;
        if (list == null || node.IsEmpty) return null;
        return list.Any(v => v.Equals(node));
    }
}