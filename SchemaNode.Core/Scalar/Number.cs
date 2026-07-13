using SchemaNode.Attribute;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Core;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Scalar;

/// <summary>
/// Represents the number scalar value type (root of the decimal type family)
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_NUMBER)]
[Meta<OfSchema>(SCHEMA_KIND_DECIMAL)]
public class Number : IScalarType<decimal>;

/// <summary>
/// Represents the double scalar value type
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_DOUBLE)]
public class Double : Number, IScalarType<double>;

/// <summary>
/// Represents the float scalar value type
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_FLOAT)]
public class Float : Number, IScalarType<float>;

/// <summary>
/// Represents the int scalar value type (root of the int type family)
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_INT)]
[Meta<ClrEquivalent>(typeof(int))]
[Meta<ClrEquivalent>(typeof(short))]
[Meta<ClrEquivalent>(typeof(sbyte))]
[Meta<ClrEquivalent>(typeof(byte))]
[Meta<ClrEquivalent>(typeof(ushort))]
[Meta<ClrEquivalent>(typeof(uint))]
[Meta<ClrEquivalent>(typeof(ulong))]
[Meta<ClrEquivalent>(typeof(Int64))]
[Meta<ClrEquivalent>(typeof(UInt16))]
[Meta<OfSchema>(SCHEMA_KIND_INT)]
public class Int : Number, IScalarType<long>;

/// <summary>
/// Represents the percent scalar value type
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_PERCENT)]
[Meta<UpLimitInt>(100)]
[Meta<LowLimitInt>(0)]
public class Percent: Float;

/// <summary>
/// Represents the year scalar value type
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_YEAR)]
[Meta<LowLimitInt>(0)]
public class Year: Int;