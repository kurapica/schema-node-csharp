using SchemaNode.Attribute;
using SchemaNode.Property.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Enum;

/// <summary>
/// The relation kind
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_RELATION}.kind")]
public enum RelationKind;