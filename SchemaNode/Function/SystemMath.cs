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
    public static decimal E() => (decimal)Math.E;
    
    [Schema]
    public static decimal Pi() => (decimal)Math.PI;

    [Schema]
    public static T Add<T>(T x, T y) where T : INumber<T> => x + y;
    
    [Schema]
    public static T AddNull<T>(T? x, T? y) where T : INumber<T> => (x ?? T.Zero) + (y ?? T.Zero);
        
    [Schema]
    public static T Divide<T>(T x, T y) where T : INumber<T> => x / y;
        
    [Schema]
    public static T Modulo<T>(T x, T y) where T : INumber<T> => x % y;
        
    [Schema]
    public static T Multiply<T>(T x, T y) where T : INumber<T> => x * y;

    [Schema]
    public static T MultiplyNull<T>(T? x, T? y) where T : INumber<T> => (x ?? T.Zero) * (y ?? T.Zero);

    [Schema]
    public static T Subtract<T>(T x, T y) where T : INumber<T> => x - y;

    [Schema]
    public static T SubtractNull<T>(T? x, T? y) where T : INumber<T> => (x ?? T.Zero) - (y ?? T.Zero);

    [Schema]
    public static decimal Percent(decimal x, decimal y, int? decimals) => Math.Round(x / y * 100, decimals ?? 2);

    [Schema]
    public static T Abs<T>(T x) where T: INumber<T> => T.Abs(x);

    [Schema]
    public static long Ceiling<T>(T x) where T : IFloatingPoint<T> => long.CreateChecked(T.Ceiling(x));

    [Schema]
    public static T Clamp<T>(T x, T min, T max) where T: INumber<T> => T.Clamp(x, min, max);

    [Schema]
    public static long Floor<T>(T x) where T: IFloatingPoint<T> => long.CreateChecked(T.Floor(x));

    [Schema]
    public static T Max<T>(T x, T y) where T : INumber<T> => T.Max(x, y);

    [Schema]
    public static T Min<T>(T x, T y) where T : INumber<T> => T.Min(x, y);

    [Schema]
    public static T PercentToNumber<T>([Schema(NS_SYSTEM_FLOAT)] float x) where T : IFloatingPoint<T>
        => T.CreateChecked(x) / T.CreateChecked(100);

    [Schema]
    public static T Round<T, T1>(T1 x, int? decimals) 
        where T: INumber<T>
        where T1: IFloatingPoint<T1>
        => T.CreateChecked(T1.Round(x, decimals ?? 0));

    [Schema]
    public static decimal ToDecimal<T>(T x) where T : INumber<T> => decimal.CreateChecked(x);

    [Schema]
    public static double ToDouble<T>(T x) where T : INumber<T> => double.CreateChecked(x);

    [Schema]
    public static long ToInteger<T>(T x) where T : INumber<T> => long.CreateChecked(x);

    [Schema]
    public static float ToSingle<T>(T x) where T : INumber<T> => float.CreateChecked(x);

    [Schema]
    public static double Acos(double x) => Math.Acos(x);

    [Schema]
    public static double Asin(double x) => Math.Asin(x);

    [Schema]
    public static double Atan(double x) => Math.Atan(x);

    [Schema]
    public static double Cos(double x) => Math.Cos(x);

    [Schema]
    public static double Sin(double x) => Math.Sin(x);

    [Schema]
    public static double Tan(double x) => Math.Tan(x);

    [Schema]
    public static double Acosh(double x) => Math.Acosh(x);

    [Schema]
    public static double Asinh(double x) => Math.Asinh(x);

    [Schema]
    public static double Atanh(double x) => Math.Atanh(x);

    [Schema]
    public static double Cosh(double x) => Math.Cosh(x);

    [Schema]
    public static double Sinh(double x) => Math.Sinh(x);

    [Schema]
    public static double Tanh(double x) => Math.Tanh(x);

    [Schema]
    public static double Exp(double x) => Math.Exp(x);

    [Schema]
    public static double Log(double x) => Math.Log(x);

    [Schema]
    public static double Sqrt(double x) => Math.Sqrt(x);

    [Schema]
    public static double Cbrt(double x) => Math.Cbrt(x);

    [Schema]
    public static double Log10(double x) => Math.Log10(x);

    [Schema]
    public static double Log2(double x) => Math.Log2(x);

    [Schema]
    public static double Pow(double x, double y) => Math.Pow(x, y);

    [Schema]
    public static long BitAnd(long x, long y) => x & y;

    [Schema]
    public static long BitLeftShift(long x, int shift) => x << shift;

    [Schema]
    public static long BitOr(long x, long y) => x | y;

    [Schema]
    public static long BitRightShift(long x, int shift) => x >> shift;

    [Schema]
    public static long BitUnary(long x) => ~x;

    [Schema]
    public static long BitXor(long x, long y) => x ^ y;
}