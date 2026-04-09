using SchemaNode.Attribute;
using SchemaNode.Property.Schema;

namespace SchemaNode.Schema;

/// <summary>
/// The schema container node, which can contain other nodes, such as scalar, struct, enum, array, etc.
/// </summary>
[Meta<SchemaKind>(nameof(NodeSchema))]
public sealed class NodeSchema: ExtensibleSchema
{
    /// <summary>
    /// The schema name
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// The schema kind
    /// </summary>
    public string Kind { get; set; } = null!;
}
