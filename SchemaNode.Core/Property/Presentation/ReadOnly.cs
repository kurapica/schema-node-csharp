using SchemaNode.Attribute;
using SchemaNode.Property.Schema;
using SchemaNode.Schema;

namespace SchemaNode.Property.Presentation;

/// <summary>
/// Readonly property for node schema, indicates the node is readonly in presentation
/// </summary>
[Meta<ForSchema>(nameof(StructFieldSchema))]
public class ReadOnly:  Property<bool>;