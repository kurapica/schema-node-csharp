using SchemaNode.Attribute;
using SchemaNode.Schema;

namespace SchemaNode.Property.Schema;

/// <summary>
/// The default value
/// </summary>
[Meta<ForSchema>(nameof(ScalarSchema), nameof(StructFieldSchema))]
public class Default: Property<object>;