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

[Meta<SchemaKind>(SCHEMA_KIND_DATE, SCHEMA_KIND_ORDER_DATE)]
[Meta<NodeSchemaKind>(SCHEMA_KIND_DATE, SCHEMA_KIND_ORDER_DATE)]
[Meta<ValueSchemaKind>(SCHEMA_KIND_DATE, SCHEMA_KIND_ORDER_DATE)]
[Meta<NodeType>(typeof(Runtime.DateType))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_DATE}.schema")]
[Meta<DateValue>]
public sealed class DateSchema : ScalarSchema
{
    /// <summary>
    /// The base date schema to inherit from
    /// </summary>
    [Meta<SchemaType>(typeof(DateType))]
    public override string? Base { get; set; }
}

/// <summary>
/// Declare date property for node schema
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_NODE)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CORE}.date")]
[Relation<Visible, Relation.Call>(NODE_SELF, NS_SYSTEM_LOGIC_EQ, $"@{nameof(NodeSchema.Kind)}", SCHEMA_KIND_DATE)]
public sealed class DateProperty : Property<DateSchema>
{
    public override bool Combine(IProperty other, ISchemaRuntime? runtime = null)
    {
        if (other is not StringProperty { Value: { } otherSchema }) return false;
        if (Value is not { } selfSchema)
        {
            SetValue(otherSchema);
            return true;
        }

        selfSchema.CombineProperties(otherSchema, runtime, SCHEMA_KIND_DATE);
        SetValue(selfSchema);
        return true;
    }
}

/// <summary>
/// Represents the date scalar type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_DATE}.type")]
[Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_IS_SCHEMA_KIND, NODE_SELF, SCHEMA_KIND_DATE)]
public class DateType : ValueType;
