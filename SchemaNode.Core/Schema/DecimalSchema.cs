using SchemaNode.Attribute;
using SchemaNode.Property;
using SchemaNode.Property.Presentation;
using SchemaNode.Property.Record;
using SchemaNode.Property.Schema;
using SchemaNode.Scalar.Schema;
using static SchemaNode.Utility.Constant;
using ValueSchemaKind = SchemaNode.Property.Record.ValueSchemaKind;

namespace SchemaNode.Schema;

[Meta<SchemaKind>(SCHEMA_KIND_DECIMAL, SCHEMA_KIND_ORDER_DECIMAL)]
[Meta<NodeSchemaKind>(SCHEMA_KIND_DECIMAL, SCHEMA_KIND_ORDER_DECIMAL)]
[Meta<ValueSchemaKind>(SCHEMA_KIND_DECIMAL, SCHEMA_KIND_ORDER_DECIMAL)]
[Meta<NodeType>(typeof(Runtime.DecimalType))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_DECIMAL}.schema")]
public sealed class DecimalSchema : ScalarSchema
{
    /// <summary>
    /// The base decimal schema to inherit from
    /// </summary>
    [Meta<SchemaType>(typeof(DecimalType))]
    public override string? Base { get; set; }
}

/// <summary>
/// Declare decimal property for node schema
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_NODE)]
[Relation<Visible>(NS_SYSTEM_LOGIC_EQ, $"${nameof(NodeSchema.Kind)}", SCHEMA_KIND_DECIMAL)]
public sealed class DecimalProperty : Property<DecimalSchema>;