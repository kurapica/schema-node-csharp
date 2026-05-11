using SchemaNode.Node;

namespace SchemaNode.Runtime;

/// <summary>
/// Runtime type for the "object" schema kind.
/// Accepts any JSON value; the actual semantic type is resolved by a Relation at runtime.
/// </summary>
public sealed class ObjectType : ScalarType
{
    /// <inheritdoc/>
    public override DataNode ParseValue(object? value) => value as AnyNode ?? new AnyNode(this, value);
}