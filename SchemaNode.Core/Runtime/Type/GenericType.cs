namespace SchemaNode.Runtime;

/// <summary>
/// The generic type do nothing
/// </summary>
internal sealed class GenericType: NodeType
{
    /// <summary>
    /// The name of the generic type parameter
    /// </summary>
    public new string Name { get; set; } = null!;
}