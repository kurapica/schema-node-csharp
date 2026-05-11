using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Attribute;
using SchemaNode.Node;
using SchemaNode.Property.Schema;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;
using NodeType = SchemaNode.Runtime.NodeType;

namespace SchemaNode.Property.Constraint;

[Meta<Alias>("lowlimit")]
[Meta<ForSchema>(SCHEMA_KIND_STRING, SCHEMA_KIND_STRUCT_FIELD)]
public class LowLimitString : Property<long>, IConstraintProperty
{
    public bool? ValidateScalar(SchemaContext context, ScalarNode node, StructNode? parent = null, Node.DataNode? overrideValue = null)
    {
        long? effectValue = overrideValue?.ToValue<long>() ?? Value;
        if (effectValue == null || node.IsEmpty) return null;
        ScalarType scalar = (ScalarType)node.NodeType;

        if (scalar.IsString)
            return node.ToValue<string>()!.Length >= effectValue;

        return null;
    }
}

[Meta<Alias>("lowlimit")]
[Meta<ForSchema>(SCHEMA_KIND_DECIMAL, SCHEMA_KIND_INT, SCHEMA_KIND_STRUCT_FIELD)]
public class LowLimitNumber : Property<ScalarNode>, IConstraintProperty
{
    public bool? ValidateScalar(SchemaContext context, ScalarNode node, StructNode? parent = null, Node.DataNode? overrideValue = null)
    {
        Node.DataNode? effectValueNode = overrideValue ?? Value;
        if (effectValueNode == null || effectValueNode.IsEmpty || node.IsEmpty) return null;
        ScalarType scalar = (ScalarType)node.NodeType;

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

[Meta<Alias>("lowlimit")]
[Meta<ForSchema>(SCHEMA_KIND_DECIMAL, SCHEMA_KIND_INT, SCHEMA_KIND_STRUCT_FIELD)]
public class LowLimitInt : Property<long>, IConstraintProperty
{
    public bool? ValidateScalar(SchemaContext context, ScalarNode node, StructNode? parent = null, Node.DataNode? overrideValue = null)
    {
        return node.ToValue<long>() >= Value;
    }
}


[Meta<Alias>("lowlimit")]
[Meta<ForSchema>(SCHEMA_KIND_DATE, SCHEMA_KIND_STRUCT_FIELD)]
public class LowLimitDate : Property<DateTimeOffset>, IConstraintProperty
{
    public bool? ValidateScalar(SchemaContext context, ScalarNode node, StructNode? parent = null, Node.DataNode? overrideValue = null)
    {
        DateTimeOffset? effectValueNode = overrideValue?.ToValue<DateTimeOffset>() ?? Value;
        if (node.IsEmpty) return null;
        ScalarType scalar = (ScalarType)node.NodeType;

        if (scalar.IsDate)
            return node.ToValue<DateTimeOffset>() >= effectValueNode;

        return null;
    }
}