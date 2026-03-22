using SchemaNode.Enum;

namespace SchemaNode.Attribute;

[AttributeUsage(AttributeTargets.Class)]
public class ConstraintAttribute(string name, string? display = null, string[]? depends = null, string[]? optionDepends = null, SchemaType[]? @for = null) : System.Attribute
{
    /// <summary>
    /// The contraint name
    /// </summary>
    public string Name { get; } = name;

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
    public SchemaType[]? For { get; } = @for;
}
