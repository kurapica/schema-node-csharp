namespace SchemaNode.Runtime;

/// <summary>
/// The node func arguments
/// </summary>
public class AppFieldNodeArgument
{
    /// <summary>
    /// The application field
    /// </summary>
    public required AppFieldType AppField { get; init; }

    /// <summary>
    /// The data field
    /// </summary>
    public string? DataField { get; init; }
}