using SchemaNode.Enum;

namespace SchemaNode.Attribute;

[AttributeUsage(AttributeTargets.Class)]
public class SchemaPropertyAttribute(SchemaType[] forSchemas, ValueSchemaType[]? forValues = null, bool includeArray = false, string? name = null, string ? display = null, string[]? depends = null, string[]? optionDepends = null, string? schemaType = null) : System.Attribute
{
    /// <summary>
    /// The Property name
    /// </summary>
    public string? Name { get; } = name;

    /// <summary>
    /// The Property display name
    /// </summary>
    public string? Display { get; } = display;

    /// <summary>
    /// The required Property names that this Property depends on
    /// </summary>
    public string[]? Depends { get; } = depends;

    /// <summary>
    /// The optional Property names that this Property depends on
    /// </summary>
    public string[]? OptionDepends { get; } = optionDepends;

    /// <summary>
    /// The schema types that this Property applies to
    /// </summary>
    public SchemaType[] ForSchemas { get; } = forSchemas;

    /// <summary>
    /// For value kinds
    /// </summary>
    public ValueSchemaType[]? ForValues { get; } = forValues;

    /// <summary>
    /// Include the value type array
    /// </summary>
    public bool? IncludeArray { get; } = includeArray;

    /// <summary>
    /// The given value schema type
    /// </summary>
    public string? SchemaType { get; } = schemaType;
}
