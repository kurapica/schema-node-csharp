using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Schema;

namespace SchemaNode.Runtime;

/// <summary>
/// Runtime type for the "date" schema kind (Date, FullDate, YearMonth).
/// </summary>
public sealed class DateType : ScalarType
{
    /// <inheritdoc/>
    public override bool IsIndexable => true;

    /// <inheritdoc/>
    public override IValueAccess Create(ISchemaContext? context, object? value = null, IValueAccess? parent = null, params IPropertyProvider[] propertyProviders) => new DateNode(context, this, value, parent, propertyProviders);
    
    /// <inheritdoc />
    protected override ScalarSchema? GetScalarSchema() => GetProperty<DateProperty>()?.Value;
}
