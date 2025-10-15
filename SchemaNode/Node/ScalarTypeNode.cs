using SchemaNode.Runtime;

namespace SchemaNode.Node;

/// <summary>
///  For common scalar node
/// </summary>
public class ScalarTypeNode : AnySchemaNode
{
    internal ScalarTypeNode(ScalarType type, object? value = null) : base(type, value)
    {
    }
}