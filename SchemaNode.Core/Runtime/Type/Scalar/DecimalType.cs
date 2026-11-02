using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Schema;

namespace SchemaNode.Runtime;

/// <summary>
/// Runtime type for the "decimal" schema kind (Number, Double, Float / Single).
/// </summary>
public sealed class DecimalType : ScalarType
{
    public override IValueAccess Create(ISchemaContext context, object? value = null, IValueAccess? parent = null, params IPropertyProvider[] propertyProviders) => new DecimalNode(context, this, value, parent, propertyProviders);
    
    /// <inheritdoc />
    protected override ScalarSchema? GetScalarSchema() => GetProperty<DecimalProperty>()?.Value;
}