using SchemaNode.Node;
using SchemaNode.Schema;

namespace SchemaNode.Runtime;

/// <summary>
/// Runtime type for the "decimal" schema kind (Number, Double, Float / Single).
/// </summary>
public sealed class DecimalType : ScalarType
{
    public override DataNode Create(IValueAccess? parent = null) => new NumericNode { Type = this, Parent = parent };
    
    /// <inheritdoc />
    protected override ScalarSchema? GetScalarSchema() => GetProperty<DecimalProperty>()?.Value;
}