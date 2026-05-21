using SchemaNode.Context;
using SchemaNode.Attribute;
using SchemaNode.Node;
using SchemaNode.Property.Schema;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Constraint;

[Meta<Alias>("uplimit")]
[Meta<ForSchema>(SCHEMA_KIND_STRING, SCHEMA_KIND_STRUCT_FIELD)]
[Meta<ForType>(typeof(StringType))]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY}.{nameof(UplimitString)}")]
public class UplimitString : Property<long>, IConstraintProperty
{
    public bool? ValidateString(SchemaContext context, StringNode node)
    {
        if (node.IsEmpty) return null;
        return (node.GetValue<string>() ?? string.Empty).Length <= Value;
    }
}


[Meta<Alias>("uplimit")]
[Meta<ForSchema>(SCHEMA_KIND_DECIMAL, SCHEMA_KIND_STRUCT_FIELD)]
[Meta<ForType>(typeof(DecimalType))]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY}.{nameof(UplimitNumber)}")]
public class UplimitNumber : Property<decimal>, IConstraintProperty
{
    public bool? ValidateNumeric(SchemaContext context, NumericNode node)
    {
        if (node.IsEmpty) return null;
        return node.GetValue<decimal>() <= Value;
    }
}

[Meta<Alias>("uplimit")]
[Meta<ForSchema>(SCHEMA_KIND_INT, SCHEMA_KIND_STRUCT_FIELD)]
[Meta<ForType>(typeof(IntType))]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY}.{nameof(UplimitInt)}")]
public class UplimitInt : Property<long>, IConstraintProperty
{
    public bool? ValidateInt(SchemaContext context, IntNode node)
    {
        if (node.IsEmpty) return null;
        return node.GetValue<long>() <= Value;
    }
}

[Meta<Alias>("uplimit")]
[Meta<ForSchema>(SCHEMA_KIND_DATE, SCHEMA_KIND_STRUCT_FIELD)]
[Meta<ForType>(typeof(DateType))]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY}.{nameof(UplimitInt)}")]
public class UplimitDate : Property<DateTimeOffset>, IConstraintProperty
{
    public bool? ValidateDate(SchemaContext context, DateNode node)
    {
        if  (node.IsEmpty) return null;
        return node.GetValue<DateTimeOffset>() <= Value;
    }
}