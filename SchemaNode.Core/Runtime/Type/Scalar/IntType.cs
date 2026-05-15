using SchemaNode.Node;
using SchemaNode.Utility;

namespace SchemaNode.Runtime;

/// <summary>
/// Runtime type for the "int" schema kind (Int, Year).
/// Year is validated as either a plain long integer or extracted from a date string.
/// </summary>
public sealed class IntType : ScalarType
{
    /// <inheritdoc/>
    public override bool IsIndexable => true;
    
    /// <inheritdoc/>
    public override DataNode ParseValue(object? value)
        => value is IntNode node && node.Type == this ? node :  new IntNode(this, value?.TryConvertTo<long>());
}
