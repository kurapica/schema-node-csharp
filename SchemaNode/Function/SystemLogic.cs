using System.Numerics;
using SchemaNode.Attribute;

namespace SchemaNode.Function;

/// <summary>
/// system.logic api
/// </summary>
[SchemaNameSpace("system.logic")]
public static class SystemLogic
{
    /// <summary>
    /// system.logic.andalso
    /// </summary>
    [SchemaFunc("system.logic.andalso")]
    public static bool AndAlso(bool a, bool b) => a && b;

    /// <summary>
    /// system.logic.between
    /// </summary>
    [SchemaFunc("system.logic.between")]
    public static bool Between<T>(T v, T min, T max, bool? includeMin, bool? includeMax) where T: INumber<T>
        =>((includeMin ?? false) ? v >= min : v > min) && ((includeMax ?? false) ? v <= max : v < max);

    /// <summary>
    /// system.logic.cond
    /// </summary>
    [SchemaFunc("system.logic.cond")]
    public static T Cond<T>(bool cond, T trueValue, T falseValue) => cond ? trueValue : falseValue;

    /// <summary>
    /// system.logic.equal
    /// </summary>
    [SchemaFunc("system.logic.equal")]
    public static bool Equal<T>(T a, T b) where T: IComparable
        => a.Equals(b);

    /// <summary>
    /// system.logic.greateequal
    /// </summary>
    [SchemaFunc("system.logic.greateequal")]
    public static bool GreateEqual<T>(T a, T b) where T: IComparable
    => a.CompareTo(b) >= 0;

    /// <summary>
    /// system.logic.greatethan
    /// </summary>
    [SchemaFunc("system.logic.greatethan")]
    public static bool GreateThan<T>(T a, T b) where T: IComparable
        => a.CompareTo(b) > 0;

    /// <summary>
    /// system.logic.isnull
    /// </summary>
    [SchemaFunc("system.logic.isnull")]
    public static bool IsNull<T>(T? a) => a is null;

    /// <summary>
    /// system.logic.lessequal
    /// </summary>
    [SchemaFunc("system.logic.lessequal")]
    public static bool LessEqual<T>(T a, T b) where T: IComparable
        => a.CompareTo(b) <= 0;

    /// <summary>
    /// system.logic.lessthan
    /// </summary>
    [SchemaFunc("system.logic.lessthan")]
    public static bool LessThan<T>(T a, T b) where T: IComparable
        => a.CompareTo(b) < 0;

    /// <summary>
    /// system.logic.not
    /// </summary>
    [SchemaFunc("system.logic.not")]
    public static bool Not(bool a) => !a;

    /// <summary>
    /// system.logic.notequal
    /// </summary>
    [SchemaFunc("system.logic.notequal")]
    public static bool NotEqual<T>(T a, T b) where T: IComparable
        => a.CompareTo(b) != 0;

    /// <summary>
    /// system.logic.notnull
    /// </summary>
    [SchemaFunc("system.logic.notnull")]
    public static bool NotNull<T>(T? a) => a is not null;

    /// <summary>
    /// system.logic.orelse
    /// </summary>
    [SchemaFunc("system.logic.orelse")]
    public static bool OrElse(bool a, bool b) => a || b;



}