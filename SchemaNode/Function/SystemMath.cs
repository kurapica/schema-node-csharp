using System.Numerics;
using SchemaNode.Attribute;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Function;

/// <summary>
/// System.Math apis
/// </summary>
[SchemaType("system.math")]
public static class SystemMath
{
    [SchemaType]
    public static decimal E() => (decimal)Math.E;
    
    [SchemaType]
    public static decimal Pi() => (decimal)Math.PI;

    [SchemaType]
    public static T Add<T>(T x, T y) where T : INumber<T> => x + y;
    
    [SchemaType]
    public static T AddNull<T>(T? x, T? y) where T : INumber<T> => (x ?? T.Zero) + (y ?? T.Zero);
        
    [SchemaType]
    public static T Divide<T>(T x, T y) where T : INumber<T> => x / y;
        
    [SchemaType]
    public static T Modulo<T>(T x, T y) where T : INumber<T> => x % y;
        
    [SchemaType]
    public static T Multiply<T>(T x, T y) where T : INumber<T> => x * y;

    [SchemaType]
    public static T MultiplyNull<T>(T? x, T? y) where T : INumber<T> => (x ?? T.Zero) * (y ?? T.Zero);

    [SchemaType]
    public static T Subtract<T>(T x, T y) where T : INumber<T> => x - y;

    [SchemaType]
    public static T SubtractNull<T>(T? x, T? y) where T : INumber<T> => (x ?? T.Zero) - (y ?? T.Zero);

    [SchemaType]
    public static decimal Percent(decimal x, decimal y, int? decimals) => Math.Round(x / y * 100, decimals ?? 2);

    [SchemaType]
    public static T Abs<T>(T x) where T: INumber<T> => T.Abs(x);

    [SchemaType]
    public static long Ceiling<T>(T x) where T : IFloatingPoint<T> => long.CreateChecked(T.Ceiling(x));

    [SchemaType]
    public static T Clamp<T>(T x, T min, T max) where T: INumber<T> => T.Clamp(x, min, max);

    [SchemaType]
    public static long Floor<T>(T x) where T: IFloatingPoint<T> => long.CreateChecked(T.Floor(x));

    [SchemaType]
    public static T Max<T>(T x, T y) where T : INumber<T> => T.Max(x, y);

    [SchemaType]
    public static T Min<T>(T x, T y) where T : INumber<T> => T.Min(x, y);

    [SchemaType]
    public static T PercentToNumber<T>([SchemaType(NS_SYSTEM_FLOAT)] float x) where T : IFloatingPoint<T>
        => T.CreateChecked(x) / T.CreateChecked(100);

    [SchemaType]
    public static T Round<T, T1>(T1 x, int? decimals) 
        where T: INumber<T>
        where T1: IFloatingPoint<T1>
        => T.CreateChecked(T1.Round(x, decimals ?? 0));

    [SchemaType]
    public static decimal ToDecimal<T>(T x) where T : INumber<T> => decimal.CreateChecked(x);

    [SchemaType]
    public static double ToDouble<T>(T x) where T : INumber<T> => double.CreateChecked(x);

    [SchemaType]
    public static long ToInteger<T>(T x) where T : INumber<T> => long.CreateChecked(x);

    [SchemaType]
    public static float ToSingle<T>(T x) where T : INumber<T> => float.CreateChecked(x);

    [SchemaType]
    public static double Acos(double x) => Math.Acos(x);

    [SchemaType]
    public static double Asin(double x) => Math.Asin(x);

    [SchemaType]
    public static double Atan(double x) => Math.Atan(x);

    [SchemaType]
    public static double Cos(double x) => Math.Cos(x);

    [SchemaType]
    public static double Sin(double x) => Math.Sin(x);

    [SchemaType]
    public static double Tan(double x) => Math.Tan(x);

    [SchemaType]
    public static double Acosh(double x) => Math.Acosh(x);

    [SchemaType]
    public static double Asinh(double x) => Math.Asinh(x);

    [SchemaType]
    public static double Atanh(double x) => Math.Atanh(x);

    [SchemaType]
    public static double Cosh(double x) => Math.Cosh(x);

    [SchemaType]
    public static double Sinh(double x) => Math.Sinh(x);

    [SchemaType]
    public static double Tanh(double x) => Math.Tanh(x);

    [SchemaType]
    public static double Exp(double x) => Math.Exp(x);

    [SchemaType]
    public static double Log(double x) => Math.Log(x);

    [SchemaType]
    public static double Sqrt(double x) => Math.Sqrt(x);

    [SchemaType]
    public static double Cbrt(double x) => Math.Cbrt(x);

    [SchemaType]
    public static double Log10(double x) => Math.Log10(x);

    [SchemaType]
    public static double Log2(double x) => Math.Log2(x);

    [SchemaType]
    public static double Pow(double x, double y) => Math.Pow(x, y);

    [SchemaType]
    public static long BitAnd(long x, long y) => x & y;

    [SchemaType]
    public static long BitLeftShift(long x, int shift) => x << shift;

    [SchemaType]
    public static long BitOr(long x, long y) => x | y;

    [SchemaType]
    public static long BitRightShift(long x, int shift) => x >> shift;

    [SchemaType]
    public static long BitUnary(long x) => ~x;

    [SchemaType]
    public static long BitXor(long x, long y) => x ^ y;
}