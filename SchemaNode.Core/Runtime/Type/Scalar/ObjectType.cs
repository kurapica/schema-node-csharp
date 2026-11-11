using SchemaNode.Context;
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
    public override IValueAccess Create(ISchemaContext? context, object? value = null, IValueAccess? parent = null, params IPropertyProvider[] propertyProviders) => new AnyNode(context, this, value, parent, propertyProviders);
    
    /// <inheritdoc />
    protected override ScalarSchema? GetScalarSchema() => null;
}