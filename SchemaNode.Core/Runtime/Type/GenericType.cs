using SchemaNode.Context;
using SchemaNode.Node;

namespace SchemaNode.Runtime;

/// <summary>
/// The generic type do nothing
/// </summary>
internal sealed class GenericType: ValueType
{
    /// <summary>
    /// The name of the generic type parameter
    /// </summary>
    public new string Name { get; set; } = null!;

    /// <summary>
    /// Use any node directly
    /// </summary>
    public override DataNode Create() => new AnyNode{ Type = this };
}