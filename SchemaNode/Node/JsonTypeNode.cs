using SchemaNode.Runtime;
using SchemaNode.Utility;

namespace SchemaNode.Node;

public class JsonTypeNode: AnySchemaNode
{
    internal JsonTypeNode(JsonType type, object? value = null) : base(type, value)
    {
    }

    public override bool Equals(AnySchemaNode other)
    {
        if (this == other) return true;
        if (other is not JsonTypeNode otherJson) return false;

        return this.ToLiteral() == otherJson.ToLiteral();
    }
}