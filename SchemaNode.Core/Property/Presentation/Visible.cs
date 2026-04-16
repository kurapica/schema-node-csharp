using SchemaNode.Attribute;
using SchemaNode.Property.Schema;
using SchemaNode.Schema;

namespace SchemaNode.Property.Presentation;

[Meta<ForSchema>(nameof(StructFieldSchema))]
public class Visible: Property<bool>;