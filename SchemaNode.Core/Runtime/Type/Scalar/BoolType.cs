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
    public override IValueAccess Create(ISchemaContext? context, object? value = null, IValueAccess? parent = null, params IPropertyProvider[] propertyProviders) => new BoolNode(context, this, value, parent, propertyProviders);

    /// <inheritdoc />
    protected override ScalarSchema? GetScalarSchema() => null;
}
