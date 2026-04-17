using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Attribute;
using SchemaNode.Node;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Constraint;

[SchemaProperty([SchemaType.Scalar, SchemaType.StructField], [ValueSchemaType.String],
    name: PROPERTY_LOWLIMIT, includeArray: true, optionDepends: [nameof(RequireProperty)])]
public class LowLimitStringProperty : SchemaProperty<long>, IConstraintProperty
{
    public bool? ValidateScalar(SchemaContext context, ScalarNode node, StructNode? parent = null, AnySchemaNode? overrideValue = null)
    {
        long? effectValue = overrideValue?.ToValue<long>() ?? Value;
        if (effectValue == null || node.IsEmpty) return null;
        ScalarType scalar = (ScalarType)node.SchemaType;

        if (scalar.IsString)
            return node.ToValue<string>()!.Length >= effectValue;

        return null;
    }
}


[SchemaProperty([SchemaType.Scalar, SchemaType.StructField], [ValueSchemaType.Number],
    name: PROPERTY_LOWLIMIT, includeArray: true, optionDepends: [nameof(RequireProperty)])]
public class LowLimitNumberProperty : SchemaProperty<ScalarNode>, IConstraintProperty
{
    public bool? ValidateScalar(SchemaContext context, ScalarNode node, StructNode? parent = null, AnySchemaNode? overrideValue = null)
    {
        AnySchemaNode? effectValueNode = overrideValue ?? Value;
        if (effectValueNode == null || effectValueNode.IsEmpty || node.IsEmpty) return null;
        ScalarType scalar = (ScalarType)node.SchemaType;

        if (scalar.IsInt)
            return node.ToValue<long>() >= effectValueNode!.ToValue<long>();

        if (scalar.IsSingle)
            return node.ToValue<float>() >= effectValueNode!.ToValue<float>();

        if (scalar.IsDouble)
            return node.ToValue<double>() >= effectValueNode!.ToValue<double>();

        if (scalar.IsNumber)
            return node.ToValue<decimal>() >= effectValueNode!.ToValue<decimal>();

        return null;
    }
}


[SchemaProperty([SchemaType.Scalar, SchemaType.StructField], [ValueSchemaType.Date],
    name: PROPERTY_LOWLIMIT, includeArray: true, optionDepends: [nameof(RequireProperty)])]
public class LowLimitDateProperty : SchemaProperty<DateTimeOffset>, IConstraintProperty
{
    public bool? ValidateScalar(SchemaContext context, ScalarNode node, StructNode? parent = null, AnySchemaNode? overrideValue = null)
    {
        DateTimeOffset? effectValueNode = overrideValue?.ToValue<DateTimeOffset>() ?? Value;
        if (effectValueNode == null || node.IsEmpty) return null;
        ScalarType scalar = (ScalarType)node.SchemaType;

        if (scalar.IsDate)
            return node.ToValue<DateTimeOffset>() >= effectValueNode;

        return null;
    }
}