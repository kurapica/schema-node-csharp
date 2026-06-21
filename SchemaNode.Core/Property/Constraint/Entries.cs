using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property.Core;
using SchemaNode.Struct;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Constraints;

[Meta<Alias>("entries")]
[Meta<ForSchema>(SCHEMA_KIND_STRING)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CONSTRAINT}.{nameof(StringEntries)}")]
public class StringEntries : Property<Entry<string>[]>, IConstraintProperty
{
    public bool? ValidateString(SchemaContext context, StringNode node)
        => Value == null || Value.Length == 0 || node.GetValue<string>() is not {} str || string.IsNullOrWhiteSpace(str) || Value.Any(entry => entry.Value == str);
}

[Meta<Alias>("entries")]
[Meta<ForSchema>(SCHEMA_KIND_INT)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CONSTRAINT}.{nameof(IntEntries)}")]
public class IntEntries : Property<Entry<long>[]>, IConstraintProperty
{
    public bool? ValidateInt(SchemaContext context, IntNode node)
        => node.IsEmpty || Value == null || Value.Length == 0 || node.GetValue<long>() is {} val && Value.Any(entry => entry.Value == val);
}