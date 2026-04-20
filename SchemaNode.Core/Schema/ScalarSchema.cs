using SchemaNode.Attribute;
using SchemaNode.Generator;
using SchemaNode.Property;
using SchemaNode.Property.Presentation;
using SchemaNode.Property.Schema;
using SchemaNode.Scalar.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Schema;

[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_SCALAR}.schema")]
[Meta<SchemaKind>("scalar", SCHEMA_KIND_ORDER_SCALAR)]
[Meta<NodeSchemaType>(typeof(Runtime.ScalarType))]
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
[Meta<ForSchema>(nameof(NodeSchema))]
[Relation<Visible>(NS_SYSTEM_LOGIC_EQ, $"${nameof(NodeSchema.Kind)}", "scalar")]
public sealed class ScalarProperty: Property<ScalarSchema>;