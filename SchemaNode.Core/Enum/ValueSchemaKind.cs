using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Enum;

/// <summary>
/// Represents the value schema kinds (schema kinds that hold actual data values)
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_SCHEMA_NODE_VALUE_KIND)]
[Meta<Record>(typeof(Property.Record.ValueSchemaKind))]
public enum ValueSchemaKind;
