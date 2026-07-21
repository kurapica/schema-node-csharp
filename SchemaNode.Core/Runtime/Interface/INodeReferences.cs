namespace SchemaNode.Runtime;

/// <summary>
/// The node reference interface, which indicates that the node has reference to other node types
/// </summary>
public interface INodeReferences
{
    /// <summary>
    /// Gets the references
    /// </summary>
    IEnumerable<NodeType> GetReferenceTypes();
}