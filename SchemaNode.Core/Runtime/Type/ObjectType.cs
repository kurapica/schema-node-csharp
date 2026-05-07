using SchemaNode.Node;
using JsonNode = SchemaNode.Node.JsonNode;

namespace SchemaNode.Runtime;

/// <summary>
/// Runtime type for the "object" schema kind.
/// Accepts any JSON value; the actual semantic type is resolved by a Relation at runtime.
/// </summary>
public sealed class ObjectType : ScalarType
{
    /// <inheritdoc/>
    public override DataNode? ParseValue(object value) => new JsonNode(this, value);
}
