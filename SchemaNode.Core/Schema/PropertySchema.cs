using SchemaNode.Attribute;
using SchemaNode.Property;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Presentation;
using SchemaNode.Property.Record;
using SchemaNode.Property.Schema;
using SchemaNode.Service;
using static SchemaNode.Utility.Constant;
using NodeSchemaKind = SchemaNode.Property.Record.NodeSchemaKind;
using SchemaType = SchemaNode.Property.Schema.SchemaType;
using String = SchemaNode.Scalar.String;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Schema;

/// <summary>
/// The property schema
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_PROPERTY, SCHEMA_KIND_ORDER_PROP)]
[Meta<NodeSchemaKind>(SCHEMA_KIND_PROPERTY, SCHEMA_KIND_ORDER_PROP)]
[Meta<SchemaGenerator>(typeof(PropertyGenerator))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY}.schema")]
public class PropertySchema: ExtensibleSchema
{
    /// <summary>
    /// The property name, such as "uplimit", "lowlimit", "pattern", etc.
    /// </summary>
    [Meta<UplimitString>(PRIMARY_KEY_MAX_LEN)]
    public string Property { get; internal set; } = string.Empty;

    /// <summary>
    /// The value type, null means use the target node type
    /// </summary>
    [Meta<SchemaType>(typeof(ValueType))]
    public string Type { get; internal set; } = string.Empty;

    /// <summary>
    /// The required property names that this depends on
    /// </summary>
    public string[]? Depends { get; set; }

    /// <summary>
    /// The other properties be overridden by this property
    /// </summary>
    public string[]? Overrides { get; set; }

    /// <summary>
    /// The schema kinds that this property applies to
    /// </summary>
    [Meta<SchemaType>($"{NS_SYSTEM_LIST}<{NS_SYSTEM_SCHEMA}.kind>")]
    public string[] ForSchemas { get; set; } = [];
    
    /// <summary>
    /// The assignable value types
    /// </summary>
    [Meta<SchemaType>($"{NS_SYSTEM_LIST}<{NS_SYSTEM_SCHEMA_NODE}.valuetype>")]
    public string[]? ForTypes { get; set; }

    /// <summary>
    /// Whether the property can't be changed by relations
    /// </summary>
    public bool? Static { get; set; }
}

/// <summary>
/// Represents the property type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY}.type")]
public class PropertyType: AnyType;

/// <summary>
/// Represents the property name
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY}.name")]
public class PropertyName : String;

/// <summary>
/// Declare the "property" property for node schema
/// </summary>
[Meta<Alias>("property")]
[Meta<ForSchema>(SCHEMA_KIND_NODE)]
[Relation<Visible>(NS_SYSTEM_LOGIC_EQ, $"${nameof(NodeSchema.Kind)}", SCHEMA_KIND_PROPERTY)]
public sealed class PropProperty: Property<PropertySchema>;