using SchemaNode.Attribute;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Property.Schema;
using SchemaNode.Scalar.Schema;
using static SchemaNode.Utility.Constant;
using ValueType = SchemaNode.Property.Schema.ValueType;

namespace SchemaNode.Schema;

[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_SCALAR}.schema")]
[Meta<AsSchemaKind>(nameof(ScalarSchema), SCHEMA_KIND_ORDER_SCALAR)]
[Meta<RuntimeType>(typeof(Runtime.ScalarType))]
[Meta<ValueType>(typeof(ScalarNode))]
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
public sealed class ScalarProperty: Property<ScalarSchema>;