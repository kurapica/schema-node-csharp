using SchemaNode.Attribute;
using SchemaNode.Property.Record;
using SchemaNode.Property.Schema;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;
using NodeType = SchemaNode.Property.Schema.NodeType;

namespace SchemaNode.Schema;

/// <summary>
/// The namespace schema, used as container for other schema nodes
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_NAMESPACE, SCHEMA_KIND_ORDER_NAMESPACE)]
[Meta<NodeSchemaKind>(SCHEMA_KIND_NAMESPACE, SCHEMA_KIND_ORDER_NAMESPACE)]
[Meta<NodeType>(typeof(NamespaceType))]
public sealed class NamespaceSchema;