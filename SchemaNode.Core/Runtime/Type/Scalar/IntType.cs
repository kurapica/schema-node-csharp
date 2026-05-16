using SchemaNode.Node;

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
    public override DataNode Create() => new IntNode { Type = this };
}
