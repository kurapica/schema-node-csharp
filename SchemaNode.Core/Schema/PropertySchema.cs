using SchemaNode.Attribute;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Core;
using SchemaNode.Property.Record;
using SchemaNode.Service;
using static SchemaNode.Utility.Constant;
using NodeSchemaKind = SchemaNode.Property.Record.NodeSchemaKind;
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
[Meta<Append>(typeof(Relations))]
public class PropertySchema: ExtensibleSchema
{
    /// <summary>
    /// The property name, such as "upLimit", "lowLimit", "pattern", etc.
    /// </summary>
    [Meta<UplimitString>(PRIMARY_KEY_MAX_LEN)]
    public string Property { get; internal set; } = string.Empty;

    /// <summary>
    /// The value type, null means use the target node type
    /// </summary>
    [Meta<SchemaType>(typeof(ValueType))]
    public string Type { get; internal set; } = string.Empty;

    /// <summary>
    /// The schema kinds that this property applies to
    /// </summary>
    [Meta<SchemaType>($"{NS_SYSTEM_LIST}<{NS_SYSTEM_SCHEMA}.kind>")]
    public string[] ForSchemas { get; set; } = [];
    
    /// <summary>
    /// Whether the property shouldn't be changed by relations
    /// </summary>
    public bool? Static { get; set; }

    /// <summary>
    /// The property is stackable, which means it can be applied multiple times and their effect is stackable not overridable
    /// </summary>
    public bool? Stackable { get; set; }
    
    /// <summary>
    /// The required property names that this depends on
    /// </summary>
    public string[]? Depends { get; set; }

    /// <summary>
    /// The other properties be overridden by this property
    /// </summary>
    public string[]? Overrides { get; set; }
}

/// <summary>
/// Represents the property type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY}.type")]
[Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_IS_SCHEMA_KIND, NODE_SELF, SCHEMA_KIND_PROPERTY)]
public class PropertyType: AnyType;

/// <summary>
/// Declare the "property" property for node schema
/// </summary>
[Meta<Alias>("property")]
[Meta<ForSchema>(SCHEMA_KIND_NODE)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CORE}.prop")]
[Relation<Visible>(NS_SYSTEM_LOGIC_EQ, $"${nameof(NodeSchema.Kind)}", SCHEMA_KIND_PROPERTY)]
public sealed class Property: Property<PropertySchema>;