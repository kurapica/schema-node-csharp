namespace SchemaNode.Attribute;

/// <summary>
/// Mark a class or struct to be used as application field
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Assembly)]
public class SchemaAppAttribute: System.Attribute
{
    /// <summary>
    /// The application name
    /// </summary>
    public string? Application { get; set; }
    
    /// <summary>
    /// The field name
    /// </summary>
    public string? Field { get; set; }
    
    /// <summary>
    /// The display
    /// </summary>
    public string? Display { get; }
    
    public bool? IncrUpdate { get; }
    
    /// <summary>
    /// Binding the application & field
    /// </summary>
    public SchemaAppAttribute(string? app = null, string? field = null, string? display = null, bool incrUpdate = false)
    {
        Application = app;
        Field = field;
        Display = display;
        IncrUpdate = incrUpdate;
    }
}
