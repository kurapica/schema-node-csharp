using SchemaNode.Context;
using SchemaNode.Attribute;
using SchemaNode.Node;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Date;

[Meta<Alias>("uplimit")]
[Meta<ForSchema>(SCHEMA_KIND_DATE, SCHEMA_KIND_DATE_DEFINE, SCHEMA_KIND_DATE_USAGE)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_DATE}.uplimit")]
public class UpLimitDate : Property<DateTimeOffset>, IConstraintProperty
{
    public bool? ValidateDate(SchemaContext context, DateNode node)
    {
        if (!HasValue || node.IsEmpty) return null;
        return node.GetValue<DateTimeOffset>() <= Value;
    }
}