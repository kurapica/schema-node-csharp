using SchemaNode.Attribute;
using SchemaNode.Property.Record;
using SchemaNode.Property.Schema;
using static SchemaNode.Utility.Constant;
using ValueSchemaKind = SchemaNode.Property.Record.ValueSchemaKind;

namespace SchemaNode.Schema;

[Meta<SchemaKind>(SCHEMA_KIND_BOOL, SCHEMA_KIND_ORDER_BOOL)]
[Meta<NodeSchemaKind>(SCHEMA_KIND_BOOL, SCHEMA_KIND_ORDER_BOOL)]
[Meta<ValueSchemaKind>(SCHEMA_KIND_BOOL, SCHEMA_KIND_ORDER_BOOL)]
[Meta<NodeType>(typeof(Runtime.BoolType))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_BOOL}.schema")]
public sealed class BoolSchema;