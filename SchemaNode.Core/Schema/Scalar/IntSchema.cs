using SchemaNode.Attribute;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Record;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;
using ValueSchemaKind = SchemaNode.Property.Record.ValueSchemaKind;

namespace SchemaNode.Schema;

[Meta<SchemaKind>(SCHEMA_KIND_INT, SCHEMA_KIND_ORDER_INT)]
[Meta<NodeSchemaKind>(SCHEMA_KIND_INT, SCHEMA_KIND_ORDER_INT)]
[Meta<ValueSchemaKind>(SCHEMA_KIND_INT, SCHEMA_KIND_ORDER_INT)]
[Meta<NodeType>(typeof(Runtime.IntType))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_INT}.schema")]
public sealed class IntSchema : ScalarSchema
{
    /// <summary>
    /// The base int schema to inherit from
    /// </summary>
    [Meta<SchemaType>(typeof(IntType))]
    public override string? Base { get; set; }
}

/// <summary>
/// Declare int property for node schema
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_NODE)]
[Relation<Visible>(NS_SYSTEM_LOGIC_EQ, $"${nameof(NodeSchema.Kind)}", SCHEMA_KIND_INT)]
public sealed class IntProperty : Property<IntSchema>;

/// <summary>
/// Represents the int scalar type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_INT}.int")]
public class IntType : AnyType;
