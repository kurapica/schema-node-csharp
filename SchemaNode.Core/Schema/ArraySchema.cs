using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Schema;
using SchemaNode.Scalar.Schema;
using static SchemaNode.Utility.Constant;
using ValueType = SchemaNode.Property.Schema.ValueType;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Schema;

/// <summary>
/// The array schema
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_ARRAY}.schema")]
[Meta<AsSchemaKind>(nameof(ArraySchema), SCHEMA_KIND_ORDER_ARRAY)]
[Meta<RuntimeType>(typeof(Runtime.ArrayType))]
[Meta<ValueType>(typeof(ArrayNode))]
[Meta<IsArray>(true)]
public sealed class ArraySchema: ExtensibleSchema
{
    /// <summary>
    /// The element type of the array.
    /// </summary>
    [Meta<SchemaType>(nameof(ElementType))]
    public string? Element { get; set; }

    /// <summary>
    /// The primary fields of the array if the element is a struct.
    /// </summary>
    public string[]? Primary { get; set; }

    /// <summary>
    /// The indexes
    /// </summary>
    public DataIndex[]? Indexes { get; set; }

    /// <summary>
    /// The data combine rule
    /// </summary>
    public DataCombine[]? Combines { get; set; }
}

/// <summary>
/// Declare array property for node schema
/// </summary>
[Meta<ForSchema>(nameof(NodeSchema))]
public sealed class ArrayProperty: Property<ArraySchema>;

/// <summary>
/// The data combine settings
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_ARRAY}.combine")]
public sealed class DataCombine
{
    /// <summary>
    /// The field
    /// </summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// The combine type
    /// </summary>
    public DataCombineType Type { get; set; } = DataCombineType.Assign;
}

[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_ARRAY}.index")]
public sealed class DataIndex
{
    /// <summary>
    /// The index name
    /// </summary>
    [Meta<UplimitStringProperty>(PRIMARY_KEY_MAX_LEN)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The index fields
    /// </summary>
    public string[] Fields { get; set; } = [];
}