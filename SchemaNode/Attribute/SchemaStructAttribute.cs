namespace SchemaNode.Attribute;

/// <summary>
/// Register a struct/class/record as system struct
/// </summary>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
public class SchemaStructAttribute: System.Attribute
{
    /// <summary>
    /// The description of the enum
    /// </summary>
    public string? Display { get; }
    
    /// <summary>
    /// The data dict type
    /// </summary>
    public string? Type { get; }
    
    /// <summary>
    /// Whether generate the array type with the given primary keys
    /// </summary>
    public string[]? Primary { get; }

    /// <summary>
    /// The constructor
    /// </summary>
    public SchemaStructAttribute(string? display = null, string? type = null, string[]? primary = null)
    {
        Display = display;
        Type = type;
        Primary = primary;
    }
}