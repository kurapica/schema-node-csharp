using SchemaNode.Attribute;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Core;
using SchemaNode.Property.Record;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;
using NodeType = SchemaNode.Property.Core.NodeType;
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
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CORE}.int")]
[Relation<Visible, Relation.Call>(NODE_SELF, NS_SYSTEM_LOGIC_EQ, $"${nameof(NodeSchema.Kind)}", SCHEMA_KIND_INT)]
public sealed class IntProperty : Property<IntSchema>
{
    public override bool Combine(IProperty other, ISchemaRuntime? runtime = null)
    {
        if (other is not StringProperty { Value: { } otherSchema }) return false;
        if (Value is not { } selfSchema)
        {
            SetValue(otherSchema);
            return true;
        }

        selfSchema.CombineProperties(otherSchema, runtime, SCHEMA_KIND_INT);
        SetValue(selfSchema);
        return true;
    }
}

/// <summary>
/// Represents the int scalar type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_INT}.type")]
[Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_IS_SCHEMA_KIND, NODE_SELF, SCHEMA_KIND_INT)]
public class IntType : ValueType;
