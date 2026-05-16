using SchemaNode.Node;

namespace SchemaNode.Runtime;

/// <summary>
/// Runtime type for the "decimal" schema kind (Number, Double, Float / Single).
/// </summary>
public sealed class DecimalType : ScalarType
{
    public override DataNode Create() => new NumericNode { Type = this };
}