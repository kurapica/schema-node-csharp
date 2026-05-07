using SchemaNode.Node;
using static SchemaNode.Utility.Extension;

namespace SchemaNode.Runtime;

/// <summary>
/// Runtime type for the "decimal" schema kind (Number, Double, Float / Single).
/// </summary>
public sealed class DecimalType : ScalarType
{
    protected override DataNode ParseValue(object? value)
        => new NumericNode(this, value?.TryConvertTo<decimal>());
}
