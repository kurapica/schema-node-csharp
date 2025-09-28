using System.Numerics;
using SchemaNode.Attribute;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Function;

/// <summary>
/// System.Math apis
/// </summary>
[SchemaNameSpace("system.math")]
public static class SystemMath
{
    [SchemaFunc("system.math.e")]
    public static decimal E() => (decimal)Math.E;
    
    [SchemaFunc("system.math.pi")]
    public static decimal Pi() => (decimal)Math.PI;

    [SchemaFunc("+")]
    public static T Add<T>(T x, T y) where T : INumber<T> => x + y;
    
    [SchemaFunc("+?")]
    public static T AddNull<T>(T? x, T? y) where T : INumber<T> => (x ?? T.Zero) + (y ?? T.Zero);
        
    [SchemaFunc("÷")]
    public static T Divide<T>(T x, T y) where T : INumber<T> => x / y;
        
    [SchemaFunc("%")]
    public static T Modulo<T>(T x, T y) where T : INumber<T> => x % y;
        
    [SchemaFunc("×")]
    public static T Multiply<T>(T x, T y) where T : INumber<T> => x * y;
    
    [SchemaFunc("×?")]
    public static T MultiplyNull<T>(T? x, T? y) where T : INumber<T> => (x ?? T.Zero) * (y ?? T.Zero);
    
    [SchemaFunc("-")]
    public static T Subtract<T>(T x, T y) where T : INumber<T> => x - y;
    
    [SchemaFunc("-?")]
    public static T SubtractNull<T>(T? x, T? y) where T : INumber<T> => (x ?? T.Zero) - (y ?? T.Zero);

    [SchemaFunc("system.math.percent")]
    public static decimal Percent(decimal x, decimal y, int? decimals) => Math.Round(x / y * 100, decimals ?? 2);
    
    [SchemaFunc("system.math.abs")]
    public static T Abs<T>(T x) where T: INumber<T> => T.Abs(x);

    [SchemaFunc("system.math.ceiling")]
    public static long Ceiling<T>(T x) where T : IFloatingPoint<T> => long.CreateChecked(T.Ceiling(x));
    
    [SchemaFunc("system.math.clamp")]
    public static T Clamp<T>(T x, T min, T max) where T: INumber<T> => T.Clamp(x, min, max);
     
    [SchemaFunc("system.math.floor")]
    public static long Floor<T>(T x) where T: IFloatingPoint<T> => long.CreateChecked(T.Floor(x));

    [SchemaFunc("system.math.max")]
    public static T Max<T>(T x, T y) where T : INumber<T> => T.Max(x, y);

    [SchemaFunc("system.math.min")]
    public static T Min<T>(T x, T y) where T : INumber<T> => T.Min(x, y);

    [SchemaFunc("system.math.percenttonum")]
    public static T PercentToNumber<T>([SchemaFuncArg(type: NS_SYSTEM_FLOAT)] float x) where T : IFloatingPoint<T>
        => T.CreateChecked(x) / T.CreateChecked(100);
        
    [SchemaFunc("system.math.round")]
    public static T Round<T, T1>(T1 x, int? decimals) 
        where T: INumber<T>
        where T1: IFloatingPoint<T1>
        => T.CreateChecked(T1.Round(x, decimals ?? 0));

    [SchemaFunc("system.math.todecimal")]
    public static decimal ToDecimal<T>(T x) where T : INumber<T> => decimal.CreateChecked(x);
    
    
    [SchemaFunc("system.math.todouble")]
    public static double ToDouble<T>(T x) where T : INumber<T> => double.CreateChecked(x);
    
    [SchemaFunc("system.math.tointeger")]
    public static long ToInteger<T>(T x) where T : INumber<T> => long.CreateChecked(x);
    
    [SchemaFunc("system.math.tosingle")]
    public static float ToSingle<T>(T x) where T : INumber<T> => float.CreateChecked(x);

    [SchemaFunc("system.math.acos")]
    public static double Acos(double x) => Math.Acos(x);
    
    [SchemaFunc("system.math.asin")]
    public static double Asin(double x) => Math.Asin(x);
        
    [SchemaFunc("system.math.atan")]
    public static double Atan(double x) => Math.Atan(x);
    
    [SchemaFunc("system.math.cos")]
    public static double Cos(double x) => Math.Cos(x);
    
    [SchemaFunc("system.math.sin")]
    public static double Sin(double x) => Math.Sin(x);
    
    [SchemaFunc("system.math.tan")]
    public static double Tan(double x) => Math.Tan(x);
    
    [SchemaFunc("system.math.acosh")]
    public static double Acosh(double x) => Math.Acosh(x);
    
    [SchemaFunc("system.math.asinh")]
    public static double Asinh(double x) => Math.Asinh(x);
    
    [SchemaFunc("system.math.atanh")]
    public static double Atanh(double x) => Math.Atanh(x);
    
    [SchemaFunc("system.math.cosh")]
    public static double Cosh(double x) => Math.Cosh(x);
    
    [SchemaFunc("system.math.sinh")]
    public static double Sinh(double x) => Math.Sinh(x);
    
    [SchemaFunc("system.math.tanh")]
    public static double Tanh(double x) => Math.Tanh(x);
    
    [SchemaFunc("system.math.exp")]
    public static double Exp(double x) => Math.Exp(x);
    
    [SchemaFunc("system.math.log")]
    public static double Log(double x) => Math.Log(x);
    
    [SchemaFunc("system.math.sqrt")]
    public static double Sqrt(double x) => Math.Sqrt(x);
    
    [SchemaFunc("system.math.cbrt")]
    public static double Cbrt(double x) => Math.Cbrt(x);
    
    [SchemaFunc("system.math.log10")]
    public static double Log10(double x) => Math.Log10(x);
    
    [SchemaFunc("system.math.log2")]
    public static double Log2(double x) => Math.Log2(x);
    
    [SchemaFunc("system.math.pow")]
    public static double Pow(double x, double y) => Math.Pow(x, y);
    
    [SchemaFunc("&")]
    public static long BitAnd(long x, long y) => x & y;
    
    [SchemaFunc("<<")]
    public static long BitLeftShift(long x, int shift) => x << shift;

    [SchemaFunc("|")]
    public static long BitOr(long x, long y) => x | y;
    
    [SchemaFunc(">>")]
    public static long BitRightShift(long x, int shift) => x >> shift;
    
    [SchemaFunc("~")]
    public static long BitUnary(long x) => ~x;
    
    [SchemaFunc("^")]
    public static long BitXor(long x, long y) => x ^ y;
}