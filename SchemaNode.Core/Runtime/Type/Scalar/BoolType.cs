using SchemaNode.Context;
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
    public override IValueAccess Create(ISchemaContext context, IValueAccess? parent = null, params IPropertyProvider[] propertyProviders) => new BoolNode(context, propertyProviders) { Type = this, Parent = parent };

    /// <inheritdoc />
    protected override ScalarSchema? GetScalarSchema() => null;
}
