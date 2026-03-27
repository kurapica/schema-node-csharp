using SchemaNode.Enum;

namespace SchemaNode.Attribute;

[AttributeUsage(AttributeTargets.Class)]
public class SchemaPropertyAttribute(SchemaType[] forSchemas, ValueSchemaType[]? forValues = null, bool includeArray = false, string? name = null, string ? display = null, string[]? depends = null, string[]? optionDepends = null, string? schemaType = null) : System.Attribute
{
    /// <summary>
    /// The contraint name
    /// </summary>
    public string? Name { get; } = name;

    /// <summary>
    /// The constraint display name
    /// </summary>
    public string? Display { get; } = display;

    /// <summary>
    /// The required constraint names that this constraint depends on
    /// </summary>
    public string[]? Depends { get; } = depends;

    /// <summary>
    /// The optional constraint names that this constraint depends on
    /// </summary>
    public string[]? OptionDepends { get; } = optionDepends;

    /// <summary>
    /// The schema types that this constraint applies to
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
