using System.Numerics;
using SchemaNode.Attribute;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;
// ReSharper disable InconsistentNaming

namespace SchemaNode.Function;

/// <summary>
/// System.Math apis
/// </summary>
[Schema(NS_SYSTEM_MATH)]
public static class SystemMath
{
    [Schema]
    [ConstantExpression(Math.E)]
    public static decimal e() => (decimal)Math.E;
    
    [Schema]
    [ConstantExpression(Math.PI)]
    public static decimal pi() => (decimal)Math.PI;

    [Schema]
    [BinaryExp(BinaryExpType.Add)]
    public static T add<T>([Default(0)] T x, [Default(0)] T y) where T : INumber<T> => x + y;
    
    [Schema]
    public static T adds<T>([Default(0)] params T[] values) where T : INumber<T>
    {
        T result = T.Zero;
        foreach (var value in values) result += value;
        return result;
    }

    [Schema]
    [Obsolete]
    public static T addnull<T>(T? x, T? y) where T : INumber<T> => (x ?? T.Zero) + (y ?? T.Zero);

    [Schema]
    [Obsolete]
    public static T addnulls<T>(params T?[] values) where T : INumber<T>
    {
        T result = T.Zero;
        foreach (var value in values)
            result += value ?? T.Zero;
        return result;
    }


    [Schema]
    [BinaryExp(BinaryExpType.Divide)]
    public static T divide<T>([Default(0)] T x, [Default(0)] T y) where T : INumber<T> => y == T.Zero ? T.Zero : x / y;


    [Schema]
    public static T divides<T>([Default(0)] params T[] values) where T : INumber<T>
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

    [Schema]
    [BinaryExp(BinaryExpType.Modulo)]
    public static T modulo<T>([Default(0)] T x, [Default(0)] T y) where T : INumber<T> => y == T.Zero ? T.Zero : x % y;

    [Schema]
    [BinaryExp(BinaryExpType.Multiply)]
    public static T multiply<T>([Default(0)] T x, [Default(0)] T y) where T : INumber<T> => x * y;

    [Schema]
    public static T multiplys<T>([Default(0)] params T[] values) where T : INumber<T>
    {
        T result = T.One;
        foreach (var value in values) result *= value;
        return result;
    }

    [Schema]
    [Obsolete]
    public static T multiplynull<T>(T? x, T? y) where T : INumber<T> => (x ?? T.Zero) * (y ?? T.Zero);

    [Schema]
    [Obsolete]
    public static T multiplynulls<T>(params T?[] values) where T : INumber<T>
    {
        T result = T.One;
        foreach (var value in values) result *= (value ?? T.Zero);
        return result;
    }

    [Schema]
    [BinaryExp(BinaryExpType.Subtract)]
    public static T subtract<T>([Default(0)] T x, [Default(0)] T y) where T : INumber<T> => x - y;

    [Schema]
    public static T subtracts<T>([Default(0)] params T[] values) where T : INumber<T>
    {
        if (values.Length == 0) return T.Zero;
        T result = values[0];
        for (int i = 1; i < values.Length; i++) result -= values[i];
        return result;
    }

    [Schema]
    [Obsolete]
    public static T subtractnull<T>(T? x, T? y) where T : INumber<T> => (x ?? T.Zero) - (y ?? T.Zero);

    [Schema]
    [Obsolete]
    public static T subtractnulls<T>(params T?[] values) where T : INumber<T>
    {
        if (values == null || values.Length == 0) return T.Zero;
        T result = values[0] ?? T.Zero;
        for (int i = 1; i < values.Length; i++) result -= (values[i] ?? T.Zero);
        return result;
    }

    [Schema]
    public static decimal percent([Default(0)] decimal x, [Default(0)] decimal y, int? decimals) 
        => Math.Round(y == 0 ? 0 : x / y * 100, decimals ?? 2);

    [Schema]
    [UnaryExp(UnaryExpType.Abs)]
    public static T abs<T>([Default(0)] T x) where T: INumber<T> => T.Abs(x);

    [Schema]
    [UnaryExp(UnaryExpType.Ceiling)]
    public static long ceiling<T>([Default(0)] T x) where T : IFloatingPoint<T> => long.CreateChecked(T.Ceiling(x));

    [Schema]
    [TernaryExp(TernaryExpType.Clamp)]
    public static T clamp<T>([Default(0)] T x, [Default(0)] T min, [Default(0)] T max) where T: INumber<T> => T.Clamp(x, min, max);

    [Schema]
    [UnaryExp(UnaryExpType.Floor)]
    public static long floor<T>([Default(0)] T x) where T: IFloatingPoint<T> => long.CreateChecked(T.Floor(x));

    [Schema]
    [BinaryExp(BinaryExpType.Max)]
    public static T max<T>([Default(0)] T x, [Default(0)] T y) where T : INumber<T> => T.Max(x, y);

    [Schema]
    public static T maxs<T>([Default(0)] params T[] values) where T : INumber<T>
    {
        if (values.Length == 0) return T.Zero;
        T result = values[0];
        for (int i = 1; i < values.Length; i++) result = T.Max(result, values[i]);
        return result;
    }

    [Schema]
    [BinaryExp(BinaryExpType.Min)]
    public static T min<T>([Default(0)] T x, [Default(0)] T y) where T : INumber<T> => T.Min(x, y);

    [Schema]
    public static T mins<T>([Default(0)] params T[] values) where T : INumber<T>
    {
        if (values.Length == 0) return T.Zero;
        T result = values[0];
        for (int i = 1; i < values.Length; i++) result = T.Min(result, values[i]);
        return result;
    }

    [Schema]
    public static T percenttonumber<T>([Schema(NS_SYSTEM_FLOAT)][Default(0)] float x) where T : IFloatingPoint<T>
        => T.CreateChecked(x) / T.CreateChecked(100);

    [Schema]
    public static T round<T, T1>([Default(0)] T1 x, int? decimals) 
        where T: INumber<T>
        where T1: IFloatingPoint<T1>
        => T.CreateChecked(T1.Round(x, decimals ?? 0));

    [Schema]
    [UnaryExp(UnaryExpType.ToDecimal)]
    public static decimal todecimal<T>([Default(0)] T x) where T : INumber<T> => decimal.CreateChecked(x);

    [Schema]
    [UnaryExp(UnaryExpType.ToDouble)]
    public static double todouble<T>([Default(0)] T x) where T : INumber<T> => double.CreateChecked(x);

    [Schema]
    [UnaryExp(UnaryExpType.ToInt)]
    public static long tointeger<T>([Default(0)] T x) where T : INumber<T> => long.CreateChecked(x);

    [Schema]
    [UnaryExp(UnaryExpType.ToSingle)]
    public static float tosingle<T>([Default(0)] T x) where T : INumber<T> => float.CreateChecked(x);

    [Schema]
    public static double acos([Default(0)] double x) => Math.Acos(x);

    [Schema]
    public static double asin([Default(0)] double x) => Math.Asin(x);

    [Schema]
    public static double atan([Default(0)] double x) => Math.Atan(x);

    [Schema]
    public static double cos([Default(0)] double x) => Math.Cos(x);

    [Schema]
    public static double sin([Default(0)] double x) => Math.Sin(x);

    [Schema]
    public static double tan([Default(0)] double x) => Math.Tan(x);

    [Schema]
    public static double acosh([Default(0)] double x) => Math.Acosh(x);

    [Schema]
    public static double asinh([Default(0)] double x) => Math.Asinh(x);

    [Schema]
    public static double atanh([Default(0)] double x) => Math.Atanh(x);

    [Schema]
    public static double cosh([Default(0)] double x) => Math.Cosh(x);

    [Schema]
    public static double sinh([Default(0)] double x) => Math.Sinh(x);

    [Schema]
    public static double tanh([Default(0)] double x) => Math.Tanh(x);

    [Schema]
    public static double exp([Default(0)] double x) => Math.Exp(x);

    [Schema]
    public static double log([Default(0)] double x) => Math.Log(x);

    [Schema]
    public static double sqrt([Default(0)] double x) => Math.Sqrt(x);

    [Schema]
    public static double cbrt([Default(0)] double x) => Math.Cbrt(x);

    [Schema]
    public static double log10([Default(0)] double x) => Math.Log10(x);

    [Schema]
    public static double log2([Default(0)] double x) => Math.Log2(x);

    [Schema]
    public static double pow([Default(0)] double x, [Default(0)] double y) => Math.Pow(x, y);

    [Schema]
    [BinaryExp(BinaryExpType.BitAnd)]
    public static long bitand([Default(0)] long x, [Default(0)] long y) => x & y;

    [Schema]
    [BinaryExp(BinaryExpType.BitLeftShift)]
    public static long bitleftshift([Default(0)] long x, [Default(0)] int shift) => x << shift;

    [Schema]
    [BinaryExp(BinaryExpType.BitOr)]
    public static long bitor([Default(0)] long x, [Default(0)] long y) => x | y;

    [Schema]
    [BinaryExp(BinaryExpType.BitRightShift)]
    public static long bitrightshift([Default(0)] long x, [Default(0)] int shift) => x >> shift;

    [Schema]
    [UnaryExp(UnaryExpType.BitUnary)]
    public static long bitunary([Default(0)] long x) => ~x;

    [Schema]
    [BinaryExp(BinaryExpType.BitXor)]
    public static long bitxor([Default(0)] long x, [Default(0)] long y) => x ^ y;
}