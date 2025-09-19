namespace SchemaNode.Attribute;

/// <summary>
/// More information for function arguments
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public class SchemaFuncArgAttribute: System.Attribute
{
    /// <summary>
    /// The description of the function
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// The data dict type
    /// </summary>
    public string? Type { get; }
    
    /// <summary>
    /// The constructor
    /// </summary>
    public SchemaFuncArgAttribute(string? desc = null, string? type = null)
    {
        Name = desc;
        Type = type;
    }
}