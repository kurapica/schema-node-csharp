using SchemaNode.Context;
using SchemaNode.Attribute;
using SchemaNode.Node;
using SchemaNode.Property.Core;
using SchemaNode.Property.Property;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Int;

[Meta<Alias>("uplimit")]
[Meta<ForSchema>(SCHEMA_KIND_INT, SCHEMA_KIND_INT_DEFINE, SCHEMA_KIND_INT_USAGE)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_INT}.uplimit")]
public class UpLimitInt : Property<long>, IConstraintProperty
{
    public bool? ValidateInt(SchemaContext context, IntNode node)
    {
        if (!HasValue || node.IsEmpty) return null;
        return node.GetValue<long>() <= Value;
    }
}
