using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Enum;

/// <summary>
/// Represents the schema kinds
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA}.kind")]
[Meta<Record>(typeof(Property.Record.SchemaKind))]
public enum SchemaKind;