namespace SchemaNode.Enum;

/// <summary>
/// Schema load state flags
/// </summary>
[Flags]
public enum SchemaLoadState
{
    /// <summary>
    /// System defined
    /// </summary>
    System = 1,
    
    /// <summary>
    /// Service defined
    /// </summary>
    Service = 2,

    /// <summary>
    /// Remote defined
    /// </summary>
    Remote = 4,
}