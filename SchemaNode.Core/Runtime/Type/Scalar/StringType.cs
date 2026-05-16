using SchemaNode.Node;
using SchemaNode.Property.Constraint;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

/// <summary>
/// Runtime type for the "string" schema kind.
/// </summary>
public sealed class StringType : ScalarType
{
    /// <inheritdoc/>
    public override bool IsIndexable => GetProperty<UplimitString>()?.GetValue<long>() < ENTITY_PRIMARY_KEY_MAX_LEN;

    /// <inheritdoc/>
    public override DataNode Create() => new StringNode { Type = this };
}
