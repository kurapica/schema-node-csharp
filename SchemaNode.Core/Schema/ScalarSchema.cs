using SchemaNode.Attribute;
using SchemaNode.Property;
using SchemaNode.Property.Presentation;
using SchemaNode.Property.Record;
using SchemaNode.Property.Schema;
using SchemaNode.Scalar.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Schema;

[Meta<SchemaKind>(SCHEMA_KIND_SCALAR, SCHEMA_KIND_ORDER_SCALAR)]
[Meta<NodeSchemaKind>(SCHEMA_KIND_SCALAR, SCHEMA_KIND_ORDER_SCALAR)]
[Meta<ValueSchemaKind>(SCHEMA_KIND_SCALAR, SCHEMA_KIND_ORDER_SCALAR)]
[Meta<NodeSchemaType>(typeof(Runtime.ScalarType))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_SCALAR}.schema")]
public sealed class ScalarSchema: ExtensibleSchema
{
    /// <summary>
    /// The base type of the scalar
    /// </summary>
    [Meta<SchemaType>(typeof(ScalarType))]
    public string? Base { get; set; }
}

/// <summary>
/// Declare scalar property for node schema
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_NODE)]
[Relation<Visible>(NS_SYSTEM_LOGIC_EQ, $"${nameof(NodeSchema.Kind)}", SCHEMA_KIND_SCALAR)]
public sealed class ScalarProperty: Property<ScalarSchema>;