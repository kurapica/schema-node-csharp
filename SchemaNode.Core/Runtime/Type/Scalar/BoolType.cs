using SchemaNode.Node;

namespace SchemaNode.Runtime;

/// <summary>
/// Runtime type for the "bool" schema kind.
/// </summary>
public sealed class BoolType : ScalarType
{
    /// <inheritdoc />
    public override bool IsIndexable => true;

    /// <inheritdoc />
    public override DataNode Create() => new BoolNode { Type = this };
}
