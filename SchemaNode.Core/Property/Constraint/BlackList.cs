using SchemaNode.Context;
using SchemaNode.Attribute;
using SchemaNode.Node;
using SchemaNode.Property.Schema;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Constraint;

[Meta<ForSchema>(SCHEMA_KIND_STRUCT_FIELD)]
[Meta<ForType>(typeof(EnumType), typeof(StringType), typeof(IntType))]
public class BlackList : Property<string[]>, IConstraintProperty
{
    public virtual bool? Validate(SchemaContext context, DataNode node)
    {
        if (Value == null || Value.Length == 0 || node.IsEmpty) return null;
        return Value.All(v => !v.Equals(node.GetValue<string>()));
    }
}