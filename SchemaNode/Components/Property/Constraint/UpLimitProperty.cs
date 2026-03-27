using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Attribute;
using SchemaNode.Node;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Components.Property.Constraint;

[SchemaProperty([SchemaType.Scalar, SchemaType.StructField], [ValueSchemaType.String], 
    name: PROPERTY_UPLIMIT, includeArray: true, optionDepends: [nameof(RequireProperty)])]
public class UplimitStringProperty : SchemaProperty<long>, IConstraintProperty
{
    public bool? ValidateScalar(SchemaContext context, ScalarTypeNode node, StructTypeNode? parent = null)
    {
        if (!HasValue || node.IsEmpty) return null;
        ScalarType scalar = (ScalarType)node.SchemaType;

        if (scalar.IsString)
            return node.ToValue<string>()!.Length <= Value!;

        return null;
    }
}


[SchemaProperty([SchemaType.Scalar, SchemaType.StructField], [ValueSchemaType.Number],
    name: PROPERTY_UPLIMIT, includeArray: true, optionDepends: [nameof(RequireProperty)])]
public class UplimitNumberProperty : SchemaProperty<ScalarTypeNode>, IConstraintProperty
{
    public bool? ValidateScalar(SchemaContext context, ScalarTypeNode node, StructTypeNode? parent = null)
    {
        if (!HasValue || node.IsEmpty) return null;
        ScalarType scalar = (ScalarType)node.SchemaType;

        if (scalar.IsInt)
            return node.ToValue<long>() <= Value!.ToValue<long>();

        if (scalar.IsSingle)
            return node.ToValue<float>() <= Value!.ToValue<float>();

        if (scalar.IsDouble)
            return node.ToValue<double>() <= Value!.ToValue<double>();

        if (scalar.IsNumber)
            return node.ToValue<decimal>() <= Value!.ToValue<decimal>();

        return null;
    }
}

[SchemaProperty([SchemaType.Scalar, SchemaType.StructField], [ValueSchemaType.Date],
    name: PROPERTY_UPLIMIT, includeArray: true, optionDepends: [nameof(RequireProperty)])]
public class UplimitDateProperty : SchemaProperty<DateTimeOffset>, IConstraintProperty
{
    public bool? ValidateScalar(SchemaContext context, ScalarTypeNode node, StructTypeNode? parent = null)
    {
        if (!HasValue || node.IsEmpty) return null;
        ScalarType scalar = (ScalarType)node.SchemaType;

        if (scalar.IsDate)
            return node.ToValue<DateTimeOffset>() <= Value;

        return null;
    }
}