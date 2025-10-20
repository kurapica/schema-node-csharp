using SchemaNode.Attribute;
using System.ComponentModel.DataAnnotations;

namespace SchemaNode.Function;

/// <summary>
/// system.conv apis
/// </summary>
[SchemaType("system.conv")]
public static class SystemConv
{
    /// <summary>
    /// Assign value
    /// </summary>
    [SchemaType]
    public static T Assign<T>(T value) => value;

    /// <summary>
    /// Gets the default value if value is null
    /// </summary>
    [SchemaType]
    public static T Default<T>(T? a, T d) => a ?? d;

    /// <summary>
    /// Return the null value of the given type
    /// </summary>
    [SchemaType]
    public static T? Null<T>() => default;
}