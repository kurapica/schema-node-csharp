using SchemaNode.Attribute;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Scalar;

/// <summary>
/// Represents the number scalar value type
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_NUMBER)]
[Meta<ClrEquivalent>(typeof(decimal))]
public class Number : IScalarType<decimal>;

/// <summary>
/// Represents the double scalar value type
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_DOUBLE)]
[Meta<ClrEquivalent>(typeof(double))]
public class Double : Number, IScalarType<double>;

/// <summary>
/// Represents the float scalar value type
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_FLOAT)]
[Meta<ClrEquivalent>(typeof(float))]
public class Float : Number, IScalarType<float>;

/// <summary>
/// Represents the int scalar value type
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_INT)]
[Meta<ClrEquivalent>(typeof(int))]
[Meta<ClrEquivalent>(typeof(long))]
[Meta<ClrEquivalent>(typeof(short))]
[Meta<ClrEquivalent>(typeof(sbyte))]
[Meta<ClrEquivalent>(typeof(byte))]
[Meta<ClrEquivalent>(typeof(ushort))]
[Meta<ClrEquivalent>(typeof(uint))]
[Meta<ClrEquivalent>(typeof(ulong))]
public class Int : Number, IScalarType<long>;

/// <summary>
/// Represents the percent scalar value type
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_PERCENT)]
[Meta<UplimitNumberProperty>(100)]
[Meta<LowLimitNumberProperty>(0)]
public class Percent: Float;

/// <summary>
/// Represents the year scalar value type
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_YEAR)]
[Meta<LowLimitNumberProperty>(0)]
public class Year: Int;