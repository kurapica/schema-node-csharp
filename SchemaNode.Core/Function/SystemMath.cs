using System.Numerics;
using SchemaNode.Attribute;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;
using SchemaNode.Property.Schema;
using SchemaNode.Property.Function;
using SchemaNode.Enum;
// ReSharper disable InconsistentNaming

namespace SchemaNode.Function;

/// <summary>
/// System.Math apis
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_MATH)]
public static class SystemMath
{
    #region Basic Arithmetic Operations
    [Meta<Arithmetic>(ArithmeticType.Add)]
    public static T add<T>([Meta<Default>(0)] params T[] values) where T : INumber<T>
    {
        T result = T.Zero;
        foreach (var value in values) result += value;
        return result;
    }
    [Meta<Arithmetic>(ArithmeticType.Subtract)]
    public static T subtract<T>([Meta<Default>(0)] params T[] values) where T : INumber<T>
    {
        if (values.Length == 0) return T.Zero;
        T result = values[0];
        for (int i = 1; i < values.Length; i++) result -= values[i];
        return result;
    }
    [Meta<Arithmetic>(ArithmeticType.Divide)]
    public static T divide<T>([Meta<Default>(0)] params T[] values) where T : INumber<T>
    {
        if (values.Length == 0) return T.One;
        T result = values[0];
        for (int i = 1; i < values.Length; i++)
        {
            if (values[i] == T.Zero) return T.Zero;
            result /= values[i];
        }
        return result;
    }
    [Meta<Arithmetic>(ArithmeticType.Modulo)]
    public static T modulo<T>([Meta<Default>(0)] T x, [Meta<Default>(0)] T y) where T : INumber<T> => y == T.Zero ? T.Zero : x % y;
    [Meta<Arithmetic>(ArithmeticType.Multiply)]
    public static T multiply<T>([Meta<Default>(0)] params T[] values) where T : INumber<T>
    {
        T result = T.One;
        foreach (var value in values) result *= value;
        return result;
    }

    #endregion

    #region Constants

    [Meta<SchemaType>($"{NS_SYSTEM_MATH}.const")]
    public static class Constants
    {
        [Meta<Constant>(Math.E)]
        public static decimal e() => (decimal)Math.E;
        [Meta<Constant>(Math.PI)]
        public static decimal pi() => (decimal)Math.PI;
    }

    #endregion

    #region Other Arithmetic Functions

    [Meta<SchemaType>($"{NS_SYSTEM_MATH}.numeric")]
    public static class Numeric
    {
        [Meta<Arithmetic>(ArithmeticType.Transform)]
        public static decimal percent([Meta<Default>(0)] decimal x, [Meta<Default>(0)] decimal y, int? decimals)
            => Math.Round(y == 0 ? 0 : x / y * 100, decimals ?? 2);
        [Meta<Arithmetic>(ArithmeticType.Transform)]
        public static T abs<T>([Meta<Default>(0)] T x) where T : INumber<T> => T.Abs(x);
        [Meta<Arithmetic>(ArithmeticType.Transform)]
        public static long ceiling<T>([Meta<Default>(0)] T x) where T : IFloatingPoint<T> => long.CreateChecked(T.Ceiling(x));
        [Meta<Arithmetic>(ArithmeticType.Transform)]
        public static T clamp<T>([Meta<Default>(0)] T x, [Meta<Default>(0)] T min, [Meta<Default>(0)] T max) where T : INumber<T> => T.Clamp(x, min, max);
        [Meta<Arithmetic>(ArithmeticType.Transform)]
        public static long floor<T>([Meta<Default>(0)] T x) where T : IFloatingPoint<T> => long.CreateChecked(T.Floor(x));
        [Meta<Arithmetic>(ArithmeticType.Max)]
        public static T max<T>([Meta<Default>(0)] params T[] values) where T : INumber<T>
        {
            if (values.Length == 0) return T.Zero;
            T result = values[0];
            for (int i = 1; i < values.Length; i++) result = T.Max(result, values[i]);
            return result;
        }
        [Meta<Arithmetic>(ArithmeticType.Min)]
        public static T min<T>([Meta<Default>(0)] params T[] values) where T : INumber<T>
        {
            if (values.Length == 0) return T.Zero;
            T result = values[0];
            for (int i = 1; i < values.Length; i++) result = T.Min(result, values[i]);
            return result;
        }
        [Meta<Arithmetic>(ArithmeticType.Transform)]
        public static T ptnum<T>([Meta<SchemaType>(NS_SYSTEM_FLOAT)][Meta<Default>(0)] float x) where T : IFloatingPoint<T>
            => T.CreateChecked(x) / T.CreateChecked(100);
        [Meta<Arithmetic>(ArithmeticType.Transform)]
        public static T round<T, T1>([Meta<Default>(0)] T1 x, int? decimals)
            where T : INumber<T>
            where T1 : IFloatingPoint<T1>
            => T.CreateChecked(T1.Round(x, decimals ?? 0));
        [Meta<Arithmetic>(ArithmeticType.Transform)]
        public static double exp([Meta<Default>(0)] double x) => Math.Exp(x);
        [Meta<Arithmetic>(ArithmeticType.Transform)]
        public static double log([Meta<Default>(0)] double x) => Math.Log(x);
        [Meta<Arithmetic>(ArithmeticType.Transform)]
        public static double sqrt([Meta<Default>(0)] double x) => Math.Sqrt(x);
        [Meta<Arithmetic>(ArithmeticType.Transform)]
        public static double cbrt([Meta<Default>(0)] double x) => Math.Cbrt(x);
        [Meta<Arithmetic>(ArithmeticType.Transform)]
        public static double log10([Meta<Default>(0)] double x) => Math.Log10(x);
        [Meta<Arithmetic>(ArithmeticType.Transform)]
        public static double log2([Meta<Default>(0)] double x) => Math.Log2(x);
        [Meta<Arithmetic>(ArithmeticType.Transform)]
        public static double pow([Meta<Default>(0)] double x, [Meta<Default>(0)] double y) => Math.Pow(x, y);
    }

    #endregion

    #region Conversion Functions

    [Meta<SchemaType>($"{NS_SYSTEM_MATH}.conversion")]
    public static class Conversion
    {
        [Meta<Arithmetic>(ArithmeticType.ToDecimal)]
        public static decimal todecimal<T>([Meta<Default>(0)] T x) where T : INumber<T> => decimal.CreateChecked(x);
        [Meta<Arithmetic>(ArithmeticType.ToDouble)]
        public static double todouble<T>([Meta<Default>(0)] T x) where T : INumber<T> => double.CreateChecked(x);
        [Meta<Arithmetic>(ArithmeticType.ToInt)]
        public static long tointeger<T>([Meta<Default>(0)] T x) where T : INumber<T> => long.CreateChecked(x);
        [Meta<Arithmetic>(ArithmeticType.ToSingle)]
        public static float tosingle<T>([Meta<Default>(0)] T x) where T : INumber<T> => float.CreateChecked(x);
    }

    #endregion

    #region Bitwise Operations

    [Meta<SchemaType>($"{NS_SYSTEM_MATH}.bitwise")]
    public static class Bitwise
    {
        [Meta<Arithmetic>(ArithmeticType.BitAnd)]
        public static long bitand([Meta<Default>(0)] long x, [Meta<Default>(0)] long y) => x & y;
        [Meta<Arithmetic>(ArithmeticType.BitLeftShift)]
        public static long bitleftshift([Meta<Default>(0)] long x, [Meta<Default>(0)] int shift) => x << shift;
        [Meta<Arithmetic>(ArithmeticType.BitOr)]
        public static long bitor([Meta<Default>(0)] long x, [Meta<Default>(0)] long y) => x | y;
        [Meta<Arithmetic>(ArithmeticType.BitRightShift)]
        public static long bitrightshift([Meta<Default>(0)] long x, [Meta<Default>(0)] int shift) => x >> shift;
        [Meta<Arithmetic>(ArithmeticType.BitUnary)]
        public static long bitunary([Meta<Default>(0)] long x) => ~x;
        [Meta<Arithmetic>(ArithmeticType.BitXor)]
        public static long bitxor([Meta<Default>(0)] long x, [Meta<Default>(0)] long y) => x ^ y;
    }

    #endregion

    #region Angle Conversion

    [Meta<SchemaType>($"{NS_SYSTEM_MATH}.trigonometry")]
    public static class Trigonometry
    {
        [Meta<Arithmetic>(ArithmeticType.Transform)]
        public static double acos([Meta<Default>(0)] double x) => Math.Acos(x);
        [Meta<Arithmetic>(ArithmeticType.Transform)]
        public static double asin([Meta<Default>(0)] double x) => Math.Asin(x);
        [Meta<Arithmetic>(ArithmeticType.Transform)]
        public static double atan([Meta<Default>(0)] double x) => Math.Atan(x);
        [Meta<Arithmetic>(ArithmeticType.Transform)]
        public static double cos([Meta<Default>(0)] double x) => Math.Cos(x);
        [Meta<Arithmetic>(ArithmeticType.Transform)]
        public static double sin([Meta<Default>(0)] double x) => Math.Sin(x);
        [Meta<Arithmetic>(ArithmeticType.Transform)]
        public static double tan([Meta<Default>(0)] double x) => Math.Tan(x);
        [Meta<Arithmetic>(ArithmeticType.Transform)]
        public static double acosh([Meta<Default>(0)] double x) => Math.Acosh(x);
        [Meta<Arithmetic>(ArithmeticType.Transform)]
        public static double asinh([Meta<Default>(0)] double x) => Math.Asinh(x);
        [Meta<Arithmetic>(ArithmeticType.Transform)]
        public static double atanh([Meta<Default>(0)] double x) => Math.Atanh(x);
        [Meta<Arithmetic>(ArithmeticType.Transform)]
        public static double cosh([Meta<Default>(0)] double x) => Math.Cosh(x);
        [Meta<Arithmetic>(ArithmeticType.Transform)]
        public static double sinh([Meta<Default>(0)] double x) => Math.Sinh(x);
        [Meta<Arithmetic>(ArithmeticType.Transform)]
        public static double tanh([Meta<Default>(0)] double x) => Math.Tanh(x);
    }

    #endregion
}