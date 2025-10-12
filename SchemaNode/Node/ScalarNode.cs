using SchemaNode.Runtime;

namespace SchemaNode.Node;

/// <summary>
///  For common scalar node
/// </summary>
public class ScalarNode : AnySchemaNode
{
    internal ScalarNode(ScalarType type, object? value = null) : base(type, value)
    {
    }
}