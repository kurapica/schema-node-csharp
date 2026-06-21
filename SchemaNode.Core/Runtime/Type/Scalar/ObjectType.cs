using SchemaNode.Node;
using SchemaNode.Schema;

namespace SchemaNode.Runtime;

/// <summary>
/// Runtime type for the "object" schema kind.
/// Accepts any JSON value; the actual semantic type is resolved by a Relation at runtime.
/// </summary>
public sealed class ObjectType : ScalarType
{
    /// <inheritdoc/>
    public override DataNode Create() => new AnyNode { Type = this };
    
    /// <inheritdoc />
    protected override ScalarSchema? GetScalarSchema() => null;
}