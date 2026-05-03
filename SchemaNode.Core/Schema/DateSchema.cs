using SchemaNode.Attribute;
using SchemaNode.Property;
using SchemaNode.Property.Presentation;
using SchemaNode.Property.Record;
using SchemaNode.Property.Schema;
using SchemaNode.Scalar.Schema;
using static SchemaNode.Utility.Constant;
using ValueSchemaKind = SchemaNode.Property.Record.ValueSchemaKind;

namespace SchemaNode.Schema;

[Meta<SchemaKind>(SCHEMA_KIND_DATE, SCHEMA_KIND_ORDER_DATE)]
[Meta<NodeSchemaKind>(SCHEMA_KIND_DATE, SCHEMA_KIND_ORDER_DATE)]
[Meta<ValueSchemaKind>(SCHEMA_KIND_DATE, SCHEMA_KIND_ORDER_DATE)]
[Meta<NodeType>(typeof(Runtime.DateType))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_DATE}.schema")]
public sealed class DateSchema : ScalarSchema
{
    /// <summary>
    /// The base date schema to inherit from
    /// </summary>
    [Meta<SchemaType>(typeof(DateScalarType))]
    public override string? Base { get; set; }
}

/// <summary>
/// Declare date property for node schema
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_NODE)]
[Relation<Visible>(NS_SYSTEM_LOGIC_EQ, $"${nameof(NodeSchema.Kind)}", SCHEMA_KIND_DATE)]
public sealed class DateProperty : Property<DateSchema>;
