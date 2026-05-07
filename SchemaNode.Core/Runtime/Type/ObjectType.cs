using SchemaNode.Node;

namespace SchemaNode.Runtime;

/// <summary>
/// Runtime type for the "object" schema kind.
/// Accepts any JSON value; the actual semantic type is resolved by a Relation at runtime.
/// </summary>
public sealed class ObjectType : ScalarType
{
    /// <inheritdoc/>
    protected override DataNode ParseValue(object? value) => new AnyNode(this, value);
}