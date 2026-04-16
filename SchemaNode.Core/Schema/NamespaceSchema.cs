using SchemaNode.Attribute;
using SchemaNode.Property;
using SchemaNode.Property.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Schema;

/// <summary>
/// The namespace schema, used as container for other schema nodes
/// </summary>
[Meta<AsSchemaKind>(nameof(NamespaceSchema), SCHEMA_KIND_ORDER_NAMESPACE)]
[Meta<AsNodeSchemaKind>(nameof(NamespaceSchema), SCHEMA_KIND_ORDER_NAMESPACE)]
public sealed class NamespaceSchema : ExtensibleSchema;

/// <summary>
/// The sub node schemas of the namespace schema
/// </summary>
[Meta<ForSchema>(nameof(NodeSchema))]
public sealed class SchemasProperty : Property<NodeSchema[]>;