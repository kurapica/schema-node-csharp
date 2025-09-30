namespace SchemaNode.Attribute;

/// <summary>
/// More information for function arguments
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public class SchemaFuncArgAttribute: System.Attribute
{
    /// <summary>
    /// The data dict type
    /// </summary>
    public string Type { get; }
    
    /// <summary>
    /// The constructor
    /// </summary>
    public SchemaFuncArgAttribute(string type)
    {
        Type = type;
    }
}