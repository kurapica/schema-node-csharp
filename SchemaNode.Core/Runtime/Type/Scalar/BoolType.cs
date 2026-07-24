using SchemaNode.Node;
using SchemaNode.Schema;

namespace SchemaNode.Runtime;

/// <summary>
/// Runtime type for the "bool" schema kind.
/// </summary>
public sealed class BoolType : ScalarType
{
    /// <inheritdoc />
    public override bool IsIndexable => true;

    /// <inheritdoc />
    public override DataNode Create(IValueAccess? parent = null) => new BoolNode { Type = this, Parent = parent };

    /// <inheritdoc />
    protected override ScalarSchema? GetScalarSchema() => null;
}
