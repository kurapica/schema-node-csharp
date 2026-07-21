namespace SchemaNode.Runtime;

/// <summary>
/// The node error interface
/// </summary>
public interface IErrorProvider
{
    /// <summary>
    /// Gets the runtime node error
    /// </summary>
    string? Error { get; }
}