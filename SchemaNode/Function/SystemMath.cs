using System.Numerics;
using SchemaNode.Attribute;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Function;

/// <summary>
/// System.Math apis
/// </summary>
[Schema("system.math")]
public static class SystemMath
{
    [Schema]
    public static decimal e() => (decimal)Math.E;
    
    [Schema]
    public static decimal pi() => (decimal)Math.PI;

    [Schema]
    public static T add<T>(T x, T y) where T : INumber<T> => x + y;
    
    [Schema]
    public static T addnull<T>(T? x, T? y) where T : INumber<T> => (x ?? T.Zero) + (y ?? T.Zero);
        
    [Schema]
    public static T divide<T>(T x, T y) where T : INumber<T> => x / y;
        
    [Schema]
    public static T modulo<T>(T x, T y) where T : INumber<T> => x % y;
        
    [Schema]
    public static T multiply<T>(T x, T y) where T : INumber<T> => x * y;

    [Schema]
    public static T multiplynull<T>(T? x, T? y) where T : INumber<T> => (x ?? T.Zero) * (y ?? T.Zero);

    [Schema]
    public static T subtract<T>(T x, T y) where T : INumber<T> => x - y;

    [Schema]
    public static T subtractnull<T>(T? x, T? y) where T : INumber<T> => (x ?? T.Zero) - (y ?? T.Zero);

    [Schema]
    public static decimal percent(decimal x, decimal y, int? decimals) => Math.Round(x / y * 100, decimals ?? 2);

    [Schema]
    public static T abs<T>(T x) where T: INumber<T> => T.Abs(x);

    [Schema]
    public static long ceiling<T>(T x) where T : IFloatingPoint<T> => long.CreateChecked(T.Ceiling(x));

    [Schema]
    public static T clamp<T>(T x, T min, T max) where T: INumber<T> => T.Clamp(x, min, max);

    [Schema]
    public static long floor<T>(T x) where T: IFloatingPoint<T> => long.CreateChecked(T.Floor(x));

    [Schema]
    public static T max<T>(T x, T y) where T : INumber<T> => T.Max(x, y);

    [Schema]
    public static T min<T>(T x, T y) where T : INumber<T> => T.Min(x, y);

    [Schema]
    public static T percenttonumber<T>([Schema(NS_SYSTEM_FLOAT)] float x) where T : IFloatingPoint<T>
        => T.CreateChecked(x) / T.CreateChecked(100);

    [Schema]
    public static T round<T, T1>(T1 x, int? decimals) 
        where T: INumber<T>
        where T1: IFloatingPoint<T1>
        => T.CreateChecked(T1.Round(x, decimals ?? 0));

    [Schema]
    public static decimal todecimal<T>(T x) where T : INumber<T> => decimal.CreateChecked(x);

    [Schema]
    public static double todouble<T>(T x) where T : INumber<T> => double.CreateChecked(x);

    [Schema]
    public static long tointeger<T>(T x) where T : INumber<T> => long.CreateChecked(x);

    [Schema]
    public static float tosingle<T>(T x) where T : INumber<T> => float.CreateChecked(x);

    [Schema]
    public static double acos(double x) => Math.Acos(x);

    [Schema]
    public static double asin(double x) => Math.Asin(x);

    [Schema]
    public static double atan(double x) => Math.Atan(x);

    [Schema]
    public static double cos(double x) => Math.Cos(x);

    [Schema]
    public static double sin(double x) => Math.Sin(x);

    [Schema]
    public static double tan(double x) => Math.Tan(x);

    [Schema]
    public static double acosh(double x) => Math.Acosh(x);

    [Schema]
    public static double asinh(double x) => Math.Asinh(x);

    [Schema]
    public static double atanh(double x) => Math.Atanh(x);

    [Schema]
    public static double cosh(double x) => Math.Cosh(x);

    [Schema]
    public static double sinh(double x) => Math.Sinh(x);

    [Schema]
    public static double tanh(double x) => Math.Tanh(x);

    [Schema]
    public static double exp(double x) => Math.Exp(x);

    [Schema]
    public static double log(double x) => Math.Log(x);

    [Schema]
    public static double sqrt(double x) => Math.Sqrt(x);

    [Schema]
    public static double cbrt(double x) => Math.Cbrt(x);

    [Schema]
    public static double log10(double x) => Math.Log10(x);

    [Schema]
    public static double log2(double x) => Math.Log2(x);

    [Schema]
    public static double pow(double x, double y) => Math.Pow(x, y);

    [Schema]
    public static long bitand(long x, long y) => x & y;

    [Schema]
    public static long bitleftshift(long x, int shift) => x << shift;

    [Schema]
    public static long bitor(long x, long y) => x | y;

    [Schema]
    public static long bitrightshift(long x, int shift) => x >> shift;

    [Schema]
    public static long bitunary(long x) => ~x;

    [Schema]
    public static long bitxor(long x, long y) => x ^ y;
}