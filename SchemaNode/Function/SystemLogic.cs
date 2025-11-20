using System.Collections;
using System.Numerics;
using System.Text.Json.Nodes;
using SchemaNode.Attribute;
using SchemaNode.Node;
using SchemaNode.Utility;

namespace SchemaNode.Function;

/// <summary>
/// system.logic api
/// </summary>
[Schema("system.logic")]
public static class SystemLogic
{
    /// <summary>
    /// system.logic.andalso
    /// </summary>
    [Schema]
    public static bool AndAlso(bool a, bool b) => a && b;

    /// <summary>
    /// system.logic.between
    /// </summary>
    [Schema]
    public static bool Between<T>(T v, T min, T max, bool? includeMin, bool? includeMax) where T: INumber<T>
        =>((includeMin ?? false) ? v >= min : v > min) && ((includeMax ?? false) ? v <= max : v < max);

    /// <summary>
    /// system.logic.cond
    /// </summary>
    [Schema]
    public static T Cond<T>(bool cond, T trueValue, T falseValue) => cond ? trueValue : falseValue;

    /// <summary>
    /// system.logic.equal
    /// </summary>
    [Schema]
    public static bool Equal<T>(T a, T b) where T: IComparable
        => a.Equals(b);

    /// <summary>
    /// system.logic.greateequal
    /// </summary>
    [Schema]
    public static bool GreateEqual<T>(T a, T b) where T: IComparable
    => a.CompareTo(b) >= 0;

    /// <summary>
    /// system.logic.greatethan
    /// </summary>
    [Schema]
    public static bool GreateThan<T>(T a, T b) where T: IComparable
        => a.CompareTo(b) > 0;

    /// <summary>
    /// system.logic.isnull
    /// </summary>
    [Schema]
    public static bool IsNull<T>(T? a) => a is null;
    
    /// <summary>
    /// system.logic.notnull
    /// </summary>
    [Schema]
    public static bool NotNull<T>(T? a) => a is not null;

    /// <summary>
    /// system.logic.isempty
    /// </summary>
    public static bool IsEmpty<T>(T? a)
    {
        if (a is null) return true;
        switch (a)
        {
            case AnySchemaNode n:
                return n.IsEmpty;
            case JsonNode j:
                return j.IsEmpty();
            case string s:
                return s.Length == 0;
            case IEnumerable e:
            {
                IEnumerator enumerator = e.GetEnumerator();
                try
                {
                    return !enumerator.MoveNext();
                }
                finally
                {
                    (enumerator as IDisposable)?.Dispose();
                }
            }
        }
        return false;
    }
    
    /// <summary>
    /// system.logic.notempty
    /// </summary>
    public static bool NotEmpty<T>(T? a) => !IsEmpty(a);

    /// <summary>
    /// system.logic.lessequal
    /// </summary>
    [Schema]
    public static bool LessEqual<T>(T a, T b) where T: IComparable
        => a.CompareTo(b) <= 0;

    /// <summary>
    /// system.logic.lessthan
    /// </summary>
    [Schema]
    public static bool LessThan<T>(T a, T b) where T: IComparable
        => a.CompareTo(b) < 0;

    /// <summary>
    /// system.logic.not
    /// </summary>
    [Schema]
    public static bool Not(bool a) => !a;

    /// <summary>
    /// system.logic.notequal
    /// </summary>
    [Schema]
    public static bool NotEqual<T>(T a, T b) where T: IComparable
        => a.CompareTo(b) != 0;

    /// <summary>
    /// system.logic.orelse
    /// </summary>
    [Schema]
    public static bool OrElse(bool a, bool b) => a || b;



}