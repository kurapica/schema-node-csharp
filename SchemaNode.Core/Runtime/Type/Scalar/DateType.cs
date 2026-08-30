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
    public override DataNode Create(IValueAccess? parent = null, IPropertyProvider? propertyProvider = null) => new DateNode { Type = this, Parent = parent, PropertyProvider = propertyProvider ?? this };
    
    /// <inheritdoc />
    protected override ScalarSchema? GetScalarSchema() => GetProperty<DateProperty>()?.Value;
}
