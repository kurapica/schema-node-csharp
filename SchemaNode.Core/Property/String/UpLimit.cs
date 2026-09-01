using SchemaNode.Context;
using SchemaNode.Attribute;
using SchemaNode.Node;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.String;

[Meta<Alias>("uplimit")]
[Meta<ForSchema>(SCHEMA_KIND_STRING, SCHEMA_KIND_STRING_DEFINE, SCHEMA_KIND_STRING_USAGE)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_STRING}.uplimit")]
public class UpLimitString : Property<long>, IConstraintProperty
{
    public bool? ValidateString(SchemaContext context, StringNode node)
    {
        if (!HasValue || node.IsEmpty) return null;
        return (node.GetValue<string>() ?? string.Empty).Length <= Value;
    }
}
