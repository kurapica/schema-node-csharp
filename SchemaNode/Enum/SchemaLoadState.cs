using SchemaNode.Attribute;

namespace SchemaNode.Enum;

/// <summary>
/// The schema load state
/// </summary>
[Flags]
public enum SchemaLoadState
{
    /// <summary>
    /// Server defined
    /// </summary>
    Server = 1,
    
    /// <summary>
    /// Custom defined
    /// </summary>
    Custom = 2,
    
    /// <summary>
    /// Front-end defined
    /// </summary>
    Frontend = 4,
    
    /// <summary>
    /// System defined
    /// </summary>
    System = 8,
    
    /// <summary>
    /// From up server
    /// </summary>
    Remote = 16,
}