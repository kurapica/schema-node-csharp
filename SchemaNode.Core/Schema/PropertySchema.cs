using SchemaNode.Attribute;
using SchemaNode.Property;
using SchemaNode.Property.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Schema;

/// <summary>
/// The property schema
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY}.schema")]
[Meta<AsSchemaKind>(nameof(PropertySchema), SCHEMA_KIND_ORDER_PROP)]
[Meta<AsNodeSchemaKind>(nameof(PropertySchema), SCHEMA_KIND_ORDER_PROP)]
public class PropertySchema
{
    /// <summary>
    /// The property name, such as "uplimit", "lowlimit", "pattern", etc.
    /// </summary>
    [Meta<SchemaType>(PRIMARY_KEY_MAX_LEN)]
    public string Property { get; internal set; } = string.Empty;

    /// <summary>
    /// The value type, null means use the target node type
    /// </summary>
    [Meta<SchemaType>(typeof(Scalar.Schema.ValueType))]
    public string? Type { get; internal set; }

    /// <summary>
    /// The required property names that this depends on
    /// </summary>
    public string[]? Depends { get; set; }

    /// <summary>
    /// The optional property names that this depends on
    /// </summary>
    public string[]? OptionDepends { get; set; }

    /// <summary>
    /// The schema types that this constraint applies to
    /// </summary>
    public string[] ForSchemas { get; set; } = [];

    /// <summary>
    /// For value kinds
    /// </summary>
    [Meta<SchemaType>(nameof(Scalar.Schema.ValueType))]
    public string[]? ForValues { get; set; }

    /// <summary>
    /// Include the value type array
    /// </summary>
    public bool? IncludeArray { get; set; }
}


/// <summary>
/// Declare the "property" property for node schema
/// </summary>
[Meta<Alias>("property")]
[Meta<ForSchema>(nameof(NodeSchema))]
public sealed class PropProperty: Property<PropertySchema>;