using SchemaNode.Attribute;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Core;
using SchemaNode.Property.Record;
using SchemaNode.Runtime;
using SchemaNode.Service;
using static SchemaNode.Utility.Constant;
using NodeSchemaKind = SchemaNode.Property.Record.NodeSchemaKind;
using NodeType = SchemaNode.Property.Core.NodeType;
using SchemaType = SchemaNode.Property.Core.SchemaType;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Schema;

/// <summary>
/// The property schema
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_PROPERTY, SCHEMA_KIND_ORDER_PROP)]
[Meta<NodeSchemaKind>(SCHEMA_KIND_PROPERTY, SCHEMA_KIND_ORDER_PROP)]
[Meta<SchemaGenerator>(typeof(PropertyGenerator))]
[Meta<NodeType>(typeof(Runtime.PropertyType))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY}.schema")]
[Meta<Attach>(SCHEMA_KIND_PROPERTY)]
[Meta<Append>(typeof(Relations))]
public class PropertySchema: PropertyOwner
{
    /// <summary>
    /// The property name, such as "upLimit", "lowLimit", "pattern", etc.
    /// </summary>
    [Meta<UpLimitString>(PRIMARY_KEY_MAX_LEN)]
    public string Property { get; set; } = string.Empty;

    /// <summary>
    /// The value type, null means use the target node type
    /// </summary>
    [Meta<SchemaType>(typeof(ValueType))]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// The schema kinds that this property applies to
    /// </summary>
    [Meta<SchemaType>($"{NS_SYSTEM_SCHEMA}.kind")]
    public string[] ForSchemas { get; set; } = [];
}

/// <summary>
/// Declare the "property" property for node schema
/// </summary>
[Meta<Alias>("property")]
[Meta<ForSchema>(SCHEMA_KIND_NODE)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CORE}.prop")]
[Meta<ReadOnly>(true)]
[Relation<Visible, Relation.Call>("property", NS_SYSTEM_LOGIC_EQ, $"@{nameof(NodeSchema.Kind)}", SCHEMA_KIND_PROPERTY)]
public sealed class PropertyProperty : Property<PropertySchema>
{
    public override bool Combine(IProperty other, ISchemaRuntime? runtime = null)
    {
        if (other is not PropertyProperty { Value: {} propertySchema }) return false;
        if (Value is not { } value)
        {
            SetValue(propertySchema);
            return true;
        }
        value.CombineProperties(propertySchema, runtime, SCHEMA_KIND_PROPERTY);
        value.ForSchemas = value.ForSchemas.Union(propertySchema.ForSchemas).Distinct().ToArray();
        return true;
    }
}

/// <summary>
/// Represents the property type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY}.type")]
[Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_IS_SCHEMA_KIND, NODE_SELF, SCHEMA_KIND_PROPERTY)]
public class PropertyType: AnyType;