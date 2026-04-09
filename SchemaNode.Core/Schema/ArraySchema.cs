using SchemaNode.Attribute;
using SchemaNode.Property.Schema.Node;

namespace SchemaNode.Schema;

/// <summary>
/// The array schema
/// </summary>
[Meta<SchemaKind>(nameof(ArraySchema))]
public sealed class ArraySchema: ExtensibleSchema
{
}
