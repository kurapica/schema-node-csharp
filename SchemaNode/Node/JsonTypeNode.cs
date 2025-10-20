using SchemaNode.Runtime;

namespace SchemaNode.Node;

public class JsonTypeNode: AnySchemaNode
{
    internal JsonTypeNode(JsonType type, object? value = null) : base(type, value)
    {
    } 
}