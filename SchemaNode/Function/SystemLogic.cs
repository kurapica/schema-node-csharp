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
    #region Terminate Functions
    
    /// <summary>
    /// system.logic.ifret
    /// if match the condition, return the value and stop the execution
    /// </summary>
    [Schema]
    [BreakExp(BreakExpType.IfRet)]
    public static T? ifret<T>(bool cond, T? value) => value;
    
    /// <summary>
    /// system.logic.ifnot
    /// if not match the condition, return the value and stop the execution
    /// </summary>
    [Schema]
    [BreakExp(BreakExpType.IfNot)]
    public static T? ifnot<T>(bool cond, T? value) => value;
    
    /// <summary>
    /// system.logic.ifnull
    /// if the value is null, return the value and stop the execution
    /// </summary>
    [Schema]
    [BreakExp(BreakExpType.IfNull)]
    public static T1? ifnull<T1, T2>(T2? val, T1? value) => value;
    
    /// <summary>
    /// system.logic.ifempty
    /// if the value is empty, return the value and stop the execution
    /// </summary>
    [Schema]
    [BreakExp(BreakExpType.IfEmpty)]
    public static T1? ifempty<T1, T2>(T2? val, T1? value) => value;
    
    #endregion

    /// <summary>
    /// system.logic.andalso
    /// </summary>
    [Schema]
    [LogicExp(LogicExpType.AndAlso)]
    public static bool andalso([Default(false)] bool a, [Default(false)] bool b) => a && b;

    /// <summary>
    /// system.logic.orelse
    /// </summary>
    [Schema]
    [LogicExp(LogicExpType.OrElse)]
    public static bool orelse([Default(false)] bool a, [Default(false)] bool b) => a || b;

    /// <summary>
    /// system.logic.not
    /// </summary>
    [Schema]
    [LogicExp(LogicExpType.Not)]
    public static bool not(bool a) => !a;

    /// <summary>
    /// system.logic.between
    /// </summary>
    [Schema]
    [LogicExp]
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
    [LogicExp(LogicExpType.IsNull)]
    public static bool isnull<T>(T? a) => a is null;
    
    /// <summary>
    /// system.logic.notnull
    /// </summary>
    [Schema]
    [LogicExp(LogicExpType.NotNull)]
    public static bool notnull<T>(T? a) => a is not null;

    /// <summary>
    /// system.logic.isempty
    /// </summary>
    [Schema]
    [LogicExp(LogicExpType.IsEmpty)]
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
    [Schema]
    [LogicExp(LogicExpType.NotEmpty)]
    public static bool notempty<T>(T? a) => !isempty(a);

    /// <summary>
    /// system.logic.equal
    /// </summary>
    [Schema]
    [LogicExp(LogicExpType.Equal)]
    public static bool equal<T>(T a, T b) where T: IComparable => a.Equals(b);

    /// <summary>
    /// system.logic.notequal
    /// </summary>
    [Schema]
    [LogicExp(LogicExpType.NotEqual)]
    public static bool notequal<T>(T a, T b) where T: IComparable => a.CompareTo(b) != 0;

    /// <summary>
    /// system.logic.greateequal
    /// </summary>
    [Schema]
    [LogicExp(LogicExpType.GreaterEqual)]
    public static bool greateequal<T>(T a, T b) where T: IComparable => a.CompareTo(b) >= 0;

    /// <summary>
    /// system.logic.greatethan
    /// </summary>
    [Schema]
    [LogicExp(LogicExpType.GreaterThan)]
    public static bool greatethan<T>(T a, T b) where T: IComparable => a.CompareTo(b) > 0;

    /// <summary>
    /// system.logic.lessequal
    /// </summary>
    [Schema]
    [LogicExp(LogicExpType.LessEqual)]
    public static bool lessequal<T>(T a, T b) where T: IComparable => a.CompareTo(b) <= 0;

    /// <summary>
    /// system.logic.lessthan
    /// </summary>
    [Schema]
    [LogicExp(LogicExpType.LessThan)]
    public static bool lessthan<T>(T a, T b) where T: IComparable => a.CompareTo(b) < 0;
}