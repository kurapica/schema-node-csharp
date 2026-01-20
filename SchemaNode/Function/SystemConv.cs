using SchemaNode.Attribute;
using SchemaNode.Runtime;
using SchemaNode.Schema;

// ReSharper disable InconsistentNaming

namespace SchemaNode.Function;

/// <summary>
/// system.conv apis
/// Language intrinsic functions (DO NOT EXTEND)
/// </summary>
[Schema("system.conv")]
public static class SystemConv
{
    /// <summary>
    /// Assign value
    /// </summary>
    [Schema]
    public static T? assign<T>(T? value) => value;

    /// <summary>
    /// Gets the default value if value is null
    /// </summary>
    [Schema]
    public static T @default<T>(T? a, T d) => a ?? d;

    /// <summary>
    /// Return the null value of the given type
    /// </summary>
    [Schema]
    public static T? @null<T>() => default;
}