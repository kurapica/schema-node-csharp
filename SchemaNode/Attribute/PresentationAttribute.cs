using SchemaNode.Enum;

namespace SchemaNode.Attribute;

[AttributeUsage(AttributeTargets.Class)]
public class PresentationAttribute(string name, string? display = null, SchemaType[]? @for = null): System.Attribute
{
    /// <summary>
    /// The presentation name
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// The presentation display name
    /// </summary>
    public string? Display { get; } = display;

    /// <summary>
    /// The schema types that this presentation applies to
    /// </summary>
    public SchemaType[]? For { get; } = @for;
}
