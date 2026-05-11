using SchemaNode.Node;
using static SchemaNode.Utility.Extension;

namespace SchemaNode.Runtime;

/// <summary>
/// Runtime type for the "decimal" schema kind (Number, Double, Float / Single).
/// </summary>
public sealed class DecimalType : ScalarType
{
    public override DataNode ParseValue(object? value)
        => value is NumericNode node && node.NodeType == this ? node :  new NumericNode(this, value?.TryConvertTo<decimal>());
}
