using SchemaNode.Attribute;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Scalar;

/// <summary>
/// Represents the number scalar value type
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_NUMBER)]
public class Number: IScalarType<Decimal>;

/// <summary>
/// Represents the double scalar value type
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_DOUBLE)]
public class Double: Number, IScalarType<double>;

/// <summary>
/// Represents the float scalar value type
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_FLOAT)]
public class Float: Number, IScalarType<float>;

/// <summary>
/// Represents the percent scalar value type
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_PERCENT)]
[Meta<UplimitNumberProperty>(100)]
[Meta<LowLimitNumberProperty>(0)]
public class Percent: Float;

/// <summary>
/// Represents the int scalar value type
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_INT)]
public class Int: Number, IScalarType<long>;

/// <summary>
/// Represents the year scalar value type
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_YEAR)]
[Meta<LowLimitNumberProperty>(0)]
public class Year: Int;