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

[Meta<SchemaKind>(SCHEMA_KIND_STRING, SCHEMA_KIND_ORDER_STRING)]
[Meta<NodeSchemaKind>(SCHEMA_KIND_STRING, SCHEMA_KIND_ORDER_STRING)]
[Meta<ValueSchemaKind>(SCHEMA_KIND_STRING, SCHEMA_KIND_ORDER_STRING)]
[Meta<NodeType>(typeof(Runtime.StringType))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_STRING}.schema")]
[Meta<StringValue>]
public sealed class StringSchema : ScalarSchema
{
    /// <summary>
    /// The base string schema to inherit from
    /// </summary>
    [Meta<SchemaType>(typeof(StringType))]
    public override string? Base { get; set; }
}

/// <summary>
/// Declare string property for node schema
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_NODE)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CORE}.string")]
[Relation<Visible, Relation.Call>(NODE_SELF, NS_SYSTEM_LOGIC_EQ, $"@{nameof(NodeSchema.Kind)}", SCHEMA_KIND_STRING)]
public sealed class StringProperty : Property<StringSchema>
{
    public override bool Combine(IProperty other, ISchemaRuntime? runtime = null)
    {
        if (other is not StringProperty { Value: { } otherSchema }) return false;
        if (Value is not { } selfSchema)
        {
            SetValue(otherSchema);
            return true;
        }

        selfSchema.CombineProperties(otherSchema, runtime, SCHEMA_KIND_STRING);
        SetValue(selfSchema);
        return true;
    }
}

/// <summary>
/// Represents the string scalar type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_STRING}.type")]
[Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_IS_SCHEMA_KIND, NODE_SELF, SCHEMA_KIND_STRING)]
public class StringType : ValueType;
