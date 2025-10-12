using SchemaNode.Runtime;

namespace SchemaNode.Node;

public class EnumNode : AnySchemaNode
{
    internal EnumNode(EnumType type, object? value = null) : base(type, value)
    {
    }
}
