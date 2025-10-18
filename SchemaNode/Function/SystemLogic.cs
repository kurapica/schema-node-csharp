using System.Numerics;
using SchemaNode.Attribute;

namespace SchemaNode.Function;

/// <summary>
/// system.logic api
/// </summary>
[SchemaType("system.logic")]
public static class SystemLogic
{
    /// <summary>
    /// system.logic.andalso
    /// </summary>
    [SchemaType]
    public static bool AndAlso(bool a, bool b) => a && b;

    /// <summary>
    /// system.logic.between
    /// </summary>
    [SchemaType]
    public static bool Between<T>(T v, T min, T max, bool? includeMin, bool? includeMax) where T: INumber<T>
        =>((includeMin ?? false) ? v >= min : v > min) && ((includeMax ?? false) ? v <= max : v < max);

    /// <summary>
    /// system.logic.cond
    /// </summary>
    [SchemaType]
    public static T Cond<T>(bool cond, T trueValue, T falseValue) => cond ? trueValue : falseValue;

    /// <summary>
    /// system.logic.equal
    /// </summary>
    [SchemaType]
    public static bool Equal<T>(T a, T b) where T: IComparable
        => a.Equals(b);

    /// <summary>
    /// system.logic.greateequal
    /// </summary>
    [SchemaType]
    public static bool GreateEqual<T>(T a, T b) where T: IComparable
    => a.CompareTo(b) >= 0;

    /// <summary>
    /// system.logic.greatethan
    /// </summary>
    [SchemaType]
    public static bool GreateThan<T>(T a, T b) where T: IComparable
        => a.CompareTo(b) > 0;

    /// <summary>
    /// system.logic.isnull
    /// </summary>
    [SchemaType]
    public static bool IsNull<T>(T? a) => a is null;

    /// <summary>
    /// system.logic.lessequal
    /// </summary>
    [SchemaType]
    public static bool LessEqual<T>(T a, T b) where T: IComparable
        => a.CompareTo(b) <= 0;

    /// <summary>
    /// system.logic.lessthan
    /// </summary>
    [SchemaType]
    public static bool LessThan<T>(T a, T b) where T: IComparable
        => a.CompareTo(b) < 0;

    /// <summary>
    /// system.logic.not
    /// </summary>
    [SchemaType]
    public static bool Not(bool a) => !a;

    /// <summary>
    /// system.logic.notequal
    /// </summary>
    [SchemaType]
    public static bool NotEqual<T>(T a, T b) where T: IComparable
        => a.CompareTo(b) != 0;

    /// <summary>
    /// system.logic.notnull
    /// </summary>
    [SchemaType]
    public static bool NotNull<T>(T? a) => a is not null;

    /// <summary>
    /// system.logic.orelse
    /// </summary>
    [SchemaType]
    public static bool OrElse(bool a, bool b) => a || b;



}