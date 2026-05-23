using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Enum;

/// <summary>
/// Represents the node schema kinds
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_NODE}.kind")]
[Meta<Record>(typeof(Property.Record.NodeSchemaKind))]
public enum NodeSchemaKind;
