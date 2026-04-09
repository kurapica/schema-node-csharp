using SchemaNode.Attribute;
using SchemaNode.Property.Schema.Node;

namespace SchemaNode.Schema;

/// <summary>
/// The struct schema
/// </summary>
[Meta<SchemaKind>(nameof(StructSchema))]
public sealed class StructSchema: ExtensibleSchema
{
}
