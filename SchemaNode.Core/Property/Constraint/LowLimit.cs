using SchemaNode.Context;
using SchemaNode.Attribute;
using SchemaNode.Node;
using SchemaNode.Property.Schema;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;
using StringType = SchemaNode.Schema.StringType;

namespace SchemaNode.Property.Constraint;

[Meta<Alias>("lowlimit")]
[Meta<ForSchema>(SCHEMA_KIND_STRING, SCHEMA_KIND_STRUCT_FIELD)]
[Meta<ForType>(typeof(StringType))]
public class LowLimitString : Property<long>, IConstraintProperty
{
    public bool? ValidateString(SchemaContext context, StringNode node)
    {
        if (node.IsEmpty) return null;
        return (node.GetValue<string>() ?? string.Empty).Length >= Value;
    }
}

[Meta<Alias>("lowlimit")]
[Meta<ForSchema>(SCHEMA_KIND_DECIMAL, SCHEMA_KIND_STRUCT_FIELD)]
[Meta<ForType>(typeof(DecimalType))]
public class LowLimitNumber : Property<decimal>, IConstraintProperty
{
    public bool? ValidateNumeric(SchemaContext context, NumericNode node)
    {
        if (node.IsEmpty) return null;
        return node.GetValue<decimal>() >= Value;
    }
}

[Meta<Alias>("lowlimit")]
[Meta<ForSchema>(SCHEMA_KIND_INT, SCHEMA_KIND_STRUCT_FIELD)]
[Meta<ForType>(typeof(IntType))]
public class LowLimitInt : Property<long>, IConstraintProperty
{
    public bool? ValidateInt(SchemaContext context, IntNode node)
    {
        if (node.IsEmpty) return null;
        return node.GetValue<long>() >= Value;
    }
}


[Meta<Alias>("lowlimit")]
[Meta<ForSchema>(SCHEMA_KIND_DATE, SCHEMA_KIND_STRUCT_FIELD)]
[Meta<ForType>(typeof(DateType))]
public class LowLimitDate : Property<DateTimeOffset>, IConstraintProperty
{
    public bool? ValidateDate(SchemaContext context, DateNode node)
    {
        if  (node.IsEmpty) return null;
        return node.GetValue<DateTimeOffset>() >= Value;
    }
}