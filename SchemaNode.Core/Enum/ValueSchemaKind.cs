using SchemaNode.Attribute;
using SchemaNode.Property.Schema;
using SchemaType = SchemaNode.Property.Schema.SchemaType;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Enum;

/// <summary>
/// The schema kinds for values
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_SCHEMA_NODE_VALUE_KIND)]
[Meta<Record>(typeof(Property.Record.ValueSchemaKind))]
public enum ValueSchemaKind;