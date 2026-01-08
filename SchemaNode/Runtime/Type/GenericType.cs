namespace SchemaNode.Runtime;

/// <summary>
/// The generic type do nothing
/// </summary>
public class GenericType: AnySchemaType
{
    /// <summary>
    /// The singleton instance
    /// </summary>
    public static GenericType Instance { get; } = new GenericType();

    /// <summary>
    /// The index of the generic type parameter
    /// </summary>
    public int? Index { get; set; }
}