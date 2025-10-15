using SchemaNode.Runtime;

namespace SchemaNode.Node;

public class EnumTypeNode : AnySchemaNode
{
    internal EnumTypeNode(EnumType type, object? value = null) : base(type, value)
    {
    }
}
