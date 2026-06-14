using SchemaNode.Node;

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

/// <summary>
/// The node error interface
/// </summary>
public interface INodeError
{
    /// <summary>
    /// Gets the runtime node error
    /// </summary>
    string? Error { get; }
}

public interface IValueTypeAccess
{
    /// <summary>
    /// Gets the access value type
    /// </summary>
    ValueType? GetAccessValueType(string path);
}

public interface IValueAccess
{
    DataNode? GetAccessValue(string path);
}