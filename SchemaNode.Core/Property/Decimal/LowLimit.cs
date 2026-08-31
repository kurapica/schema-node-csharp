using SchemaNode.Context;
using SchemaNode.Attribute;
using SchemaNode.Node;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Decimal;

[Meta<Alias>("lowlimit")]
[Meta<ForSchema>(SCHEMA_KIND_DECIMAL)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_DECIMAL}.lowlimit")]
public class LowLimitNumber : Property<decimal>, IConstraintProperty
{
    public bool? ValidateNumeric(SchemaContext context, DecimalNode node)
    {
        if (!HasValue || node.IsEmpty) return null;
        return node.GetValue<decimal>() >= Value;
    }
}
