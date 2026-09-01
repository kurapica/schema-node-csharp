using SchemaNode.Context;
using SchemaNode.Attribute;
using SchemaNode.Node;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Decimal;

[Meta<Alias>("uplimit")]
[Meta<ForSchema>(SCHEMA_KIND_DECIMAL, SCHEMA_KIND_DECIMAL_DEFINE, SCHEMA_KIND_DECIMAL_USAGE)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_DECIMAL}.uplimit")]
public class UpLimitNumber : Property<decimal>, IConstraintProperty
{
    public bool? ValidateNumeric(SchemaContext context, DecimalNode node)
    {
        if (!HasValue || node.IsEmpty) return null;
        return node.GetValue<decimal>() <= Value;
    }
}