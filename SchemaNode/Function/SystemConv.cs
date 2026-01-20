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

    /// <summary>
    /// Generate a dynamic schema type name, only used in frontend for json field type, no backend validation
    /// </summary>=
    public static string toschematype(NodeSchema schema) => $"_dynamic_.{Guid.NewGuid()}";
    
    /// <summary>
    /// Generate a dynamic struct type name, only used in frontend for json field type, no backend validation
    /// </summary>
    public static string tostructtype(StructFieldConfig[] fields) => $"_dynamic_struct_.{Guid.NewGuid()}";
}