using System.Collections;
using System.Numerics;
using System.Text.Json.Nodes;
using SchemaNode.Attribute;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
// ReSharper disable InconsistentNaming

namespace SchemaNode.Function;

/// <summary>
/// system.logic api
/// </summary>
[Schema(NS_SYSTEM_LOGIC)]
public static class SystemLogic
{
    /// <summary>
    /// system.logic.and
    /// </summary>
    [Schema]
    [Logic(LogicType.AndAlso)]
    public static bool and([Default(false)] bool a, [Default(false)] bool b) => a && b;

    /// <summary>
    /// system.logic.or
    /// </summary>
    [Schema]
    [Logic(LogicType.OrElse)]
    public static bool or([Default(false)] bool a, [Default(false)] bool b) => a || b;

    /// <summary>
    /// system.logic.not
    /// </summary>
    [Schema]
    [Logic(LogicType.Not)]
    public static bool not(bool a) => !a;

    /// <summary>
    /// system.logic.between
    /// </summary>
    [Schema]
    public static bool between<T>(T v, T min, T max, bool? includeMin, bool? includeMax) where T: INumber<T>
        =>((includeMin ?? false) ? v >= min : v > min) && ((includeMax ?? false) ? v <= max : v < max);

    /// <summary>
    /// system.logic.cond
    /// </summary>
    [Schema]
    public static T cond<T>([Default(false)] bool cond, T trueValue, T falseValue) => cond ? trueValue : falseValue;

    /// <summary>
    /// system.logic.isnull
    /// </summary>
    [Schema]
    [Logic(LogicType.IsNull)]
    public static bool isnull<T>(T? a) => a is null;
    
    /// <summary>
    /// system.logic.notnull
    /// </summary>
    [Schema]
    [Logic(LogicType.NotNull)]
    public static bool notnull<T>(T? a) => a is not null;

    /// <summary>
    /// system.logic.isempty
    /// </summary>
    [Schema]
    [Logic(LogicType.IsEmpty, true)]
    public static bool isempty(object? a)
    {
        if (a is null) return true;
        switch (a)
        {
            case AnySchemaNode n:
                return n.IsEmpty;
            case JsonNode j:
                return j.IsEmpty();
            case string s:
                return string.IsNullOrWhiteSpace(s);
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
    [Schema]
    [Logic(LogicType.NotEmpty, true)]
    public static bool notempty(object? a) => !isempty(a);

    /// <summary>
    /// system.logic.eq
    /// </summary>
    [Schema]
    [Logic(LogicType.Equal)]
    public static bool eq<T>(T? a, T? b) where T: IComparable => a == null && b == null || a != null && a.Equals(b);

    /// <summary>
    /// system.logic.neq
    /// </summary>
    [Schema]
    [Logic(LogicType.NotEqual)]
    public static bool neq<T>(T? a, T? b) where T : IComparable => !eq(a, b);

    /// <summary>
    /// system.logic.ge
    /// </summary>
    [Schema]
    [Logic(LogicType.GreaterEqual)]
    public static bool ge<T>(T? a, T? b) where T: IComparable => a != null && b != null && a.CompareTo(b) >= 0;

    /// <summary>
    /// system.logic.gt
    /// </summary>
    [Schema]
    [Logic(LogicType.GreaterThan)]
    public static bool gt<T>(T? a, T? b) where T: IComparable => a != null && b != null && a.CompareTo(b) > 0;

    /// <summary>
    /// system.logic.le
    /// </summary>
    [Schema]
    [Logic(LogicType.LessEqual)]
    public static bool le<T>(T? a, T? b) where T: IComparable => a != null && b != null && a.CompareTo(b) <= 0;

    /// <summary>
    /// system.logic.lt
    /// </summary>
    [Schema]
    [Logic(LogicType.LessThan)]
    public static bool lt<T>(T? a, T? b) where T: IComparable => a != null && b != null && a.CompareTo(b) < 0;
}