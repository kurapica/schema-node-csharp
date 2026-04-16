using SchemaNode.Attribute;
using SchemaNode.Property;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Schema;

[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_SCALAR}.schema")]
[Meta<AsSchemaKind>(nameof(ScalarSchema), SCHEMA_KIND_ORDER_SCALAR)]
[Meta<AsNodeSchemaKind>(nameof(ScalarSchema), SCHEMA_KIND_ORDER_SCALAR)]
public sealed class ScalarSchema: ExtensibleSchema
{
    /// <summary>
    /// The base type of the scalar
    /// </summary>
    [Meta<SchemaType>(typeof(Scalar.Schema.ScalarType))]
    public string? Base { get; set; }
}

/// <summary>
/// Declare scalar property for node schema
/// </summary>
[Meta<ForSchema>(nameof(NodeSchema))]
public sealed class ScalarProperty: Property<ScalarSchema>;