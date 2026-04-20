using SchemaNode.Attribute;

namespace SchemaNode.Property.Schema;

/// <summary>
/// The record for the relation kind
/// </summary>
[Meta<Record>(typeof(Enum.RelationKind))]
public class RelationKind : RecordProperty<string>;