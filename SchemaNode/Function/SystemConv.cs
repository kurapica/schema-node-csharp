using SchemaNode.Attribute;
using System.ComponentModel.DataAnnotations;

namespace SchemaNode.Function;

/// <summary>
/// system.conv apis
/// </summary>
[Schema("system.conv")]
public static class SystemConv
{
    /// <summary>
    /// Assign value
    /// </summary>
    [Schema]
    public static T assign<T>(T value) => value;

    /// <summary>
    /// Gets the default value if value is null
    /// </summary>
    [Schema]
    public static T Default<T>(T? a, T d) => a ?? d;

    /// <summary>
    /// Return the null value of the given type
    /// </summary>
    [Schema]
    public static T? Null<T>() => default;
}