namespace SchemaNode.Enum;

/// <summary>
/// The schema load state
/// </summary>
[Flags]
public enum SchemaLoadState
{
    Server = 1,
    Custom = 2,
    Frontend = 4,
    System = 8,
}