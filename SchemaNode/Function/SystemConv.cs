using SchemaNode.Attribute;

namespace SchemaNode.Function;

/// <summary>
/// system.conv apis
/// </summary>
[SchemaNameSpace("system.conv")]
public static class SystemConv
{
    /// <summary>
    /// Assign value
    /// </summary>
    [SchemaFunc("=")]
    public static T Assign<T>(T value) => value;

    /// <summary>
    /// Gets the default value if value is null
    /// </summary>
    [SchemaFunc("system.conv.default")]
    public static T Default<T>(T? a, T d) => a ?? d;
    
    /// <summary>
    /// Return the null value of the given type
    /// </summary>
    [SchemaFunc("system.conv.null")]
    public static T? Null<T>() => default;
}