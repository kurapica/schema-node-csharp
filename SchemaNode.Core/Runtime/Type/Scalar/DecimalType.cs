using SchemaNode.Node;
using SchemaNode.Schema;

namespace SchemaNode.Runtime;

/// <summary>
/// Runtime type for the "decimal" schema kind (Number, Double, Float / Single).
/// </summary>
public sealed class DecimalType : ScalarType
{
    public override DataNode Create(IValueAccess? parent = null, IPropertyProvider? propertyProvider = null) => new DecimalNode { Type = this, Parent = parent, PropertyProvider = propertyProvider ?? this };
    
    /// <inheritdoc />
    protected override ScalarSchema? GetScalarSchema() => GetProperty<DecimalProperty>()?.Value;
}