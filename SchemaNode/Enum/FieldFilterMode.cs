namespace SchemaNode.Enum;

/// <summary>
/// The app data field filter mode
/// </summary>
public enum FieldFilterMode
{
    /// <summary>
    /// Exactly match
    /// </summary>
    Exactly = 1,
    
    /// <summary>
    /// Prefix match
    /// </summary>
    Prefix = 2,
    
    /// <summary>
    /// Suffix match
    /// </summary>
    Suffix = 3,
    
    /// <summary>
    /// Like / contains match
    /// </summary>
    Contains = 4,
}