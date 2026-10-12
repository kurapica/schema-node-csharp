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
    public override IValueAccess Create(IValueAccess? parent = null, IPropertyProvider? propertyProvider = null) => new BoolNode { Type = this, Parent = parent, PropertyProvider = propertyProvider ?? this };

    /// <inheritdoc />
    protected override ScalarSchema? GetScalarSchema() => null;
}
