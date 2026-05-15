using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Property;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Presentation;
using SchemaNode.Property.Schema;
using static SchemaNode.Utility.Constant;
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
}

/// <summary>
/// Declare array property for node schema
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_NODE)]
[Relation<Visible>(NS_SYSTEM_LOGIC_EQ, $"${nameof(NodeSchema.Kind)}", SCHEMA_KIND_ARRAY)]
public sealed class ArrayProperty: Property<ArraySchema>;

/// <summary>
/// Represents the array type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_ARRAY}.type")]
public class ArrayType: AnyType;

/// <summary>
/// Represents the non-array type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_ARRAY}.elementtype")]
public class ElementType : AnyType;
