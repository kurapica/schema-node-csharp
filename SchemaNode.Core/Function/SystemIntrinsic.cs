using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;

// ReSharper disable InconsistentNaming

namespace SchemaNode.Function;

/// <summary>
/// system.intrinsic apis
/// Language intrinsic functions
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_INTRINSIC)]
public static class SystemIntrinsic
{
    #region Assignment

    /// <summary>
    /// Assign value
    /// </summary>
    public static T? assign<T>(T? value) => value;

    /// <summary>
    /// Gets the default value if value is null
    /// </summary>
    public static T @default<T>(T? a, T d) => a ?? d;

    /// <summary>
    /// Return the null value of the given type
    /// </summary>
    public static T? @null<T>() => default;

    #endregion

    #region Terminate

    /// <summary>
    /// system.intrinsic.ifret
    /// if contains the condition, return the value and stop the execution
    /// </summary>
    public static T? ifret<T>(bool cond, T? value) => value;

    /// <summary>
    /// system.intrinsic.ifnot
    /// if not contains the condition, return the value and stop the execution
    /// </summary>
    public static T? ifnot<T>(bool cond, T? value) => value;

    /// <summary>
    /// system.intrinsic.ifnull
    /// if the value is null, return the value and stop the execution
    /// </summary>
    public static T1? ifnull<T1, T2>(T2? val, T1? value) => value;

    /// <summary>
    /// system.intrinsic.ifempty
    /// if the value is empty, return the value and stop the execution
    /// </summary>
    public static T1? ifempty<T1, T2>(T2? val, T1? value) => value;

    #endregion
}