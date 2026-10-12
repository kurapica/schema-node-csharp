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
    public override IValueAccess Create(IValueAccess? parent = null, IPropertyProvider? propertyProvider = null) => new AnyNode { Type = this, Parent = parent, PropertyProvider = propertyProvider ?? this };
    
    /// <inheritdoc />
    protected override ScalarSchema? GetScalarSchema() => null;
}