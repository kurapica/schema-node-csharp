using SchemaNode.Attribute;
using SchemaNode.Generator;
using SchemaNode.Property;
using SchemaNode.Property.Presentation;
using SchemaNode.Property.Schema;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Schema;

/// <summary>
/// The namespace schema, used as container for other schema nodes
/// </summary>
[Meta<SchemaKind>("namespace", SCHEMA_KIND_ORDER_NAMESPACE)]
[Meta<NodeSchemaType>(typeof(NamespaceType))]
[Meta<SchemaGenerator>(typeof(NamespaceGenerator))]
public sealed class NamespaceSchema : ExtensibleSchema;

/// <summary>
/// The sub node schemas of the namespace schema
/// </summary>
[Meta<ForSchema>(nameof(NodeSchema))]
[Relation<Visible>(NS_SYSTEM_LOGIC_EQ, $"${nameof(NodeSchema.Kind)}", "namespace")]
public sealed class SchemasProperty : Property<NodeSchema[]>;