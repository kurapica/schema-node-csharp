using System.Data;
using SchemaNode.Attribute;
using SchemaType = SchemaNode.Property.Schema.SchemaType;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Enum;

/// <summary>
/// The schema kinds for values
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_SCHEMA_NODE_VALUE_KIND)]
public enum ValueSchemaKind;