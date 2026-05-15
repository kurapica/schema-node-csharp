using SchemaNode.Node;
using static SchemaNode.Utility.Extension;

namespace SchemaNode.Runtime;

/// <summary>
/// Runtime type for the "date" schema kind (Date, FullDate, YearMonth).
/// </summary>
public sealed class DateType : ScalarType
{
    /// <inheritdoc/>
    public override bool IsIndexable => true;

    /// <inheritdoc/>
    public override DataNode ParseValue(object? value)
        => value is DateNode node && node.Type == this ? node :  new DateNode(this, value?.TryConvertTo<DateTimeOffset>());
}
