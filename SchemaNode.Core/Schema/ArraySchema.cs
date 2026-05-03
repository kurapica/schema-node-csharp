using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Property;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Presentation;
using SchemaNode.Property.Schema;
using SchemaNode.Scalar.Schema;
using static SchemaNode.Utility.Constant;
using ArrayType = SchemaNode.Runtime.ArrayType;
using NodeSchemaKind = SchemaNode.Property.Record.NodeSchemaKind;
using ValueSchemaKind = SchemaNode.Property.Record.ValueSchemaKind;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Schema;

/// <summary>
/// The array schema
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_ARRAY, SCHEMA_KIND_ORDER_ARRAY)]
[Meta<NodeSchemaKind>(SCHEMA_KIND_ARRAY, SCHEMA_KIND_ORDER_ARRAY)]
[Meta<ValueSchemaKind>(SCHEMA_KIND_ARRAY, SCHEMA_KIND_ORDER_ARRAY)]
[Meta<NodeType>(typeof(ArrayType))]
[Meta<IsArray>(true)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_ARRAY}.schema")]
public sealed class ArraySchema: ExtensibleSchema
{
    /// <summary>
    /// The element type of the array.
    /// </summary>
    [Meta<SchemaType>(nameof(ElementType))]
    public required string Element { get; set; }

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
[Meta<ForSchema>(SCHEMA_KIND_NODE)]
[Relation<Visible>(NS_SYSTEM_LOGIC_EQ, $"${nameof(NodeSchema.Kind)}", SCHEMA_KIND_ARRAY)]
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
    [Meta<UplimitString>(PRIMARY_KEY_MAX_LEN)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The index fields
    /// </summary>
    public string[] Fields { get; set; } = [];
    
    /// <summary>
    /// Whether the index is unique
    /// </summary>
    public bool IsUnique { get; set; }
}