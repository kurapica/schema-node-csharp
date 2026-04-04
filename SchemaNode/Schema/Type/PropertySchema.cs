using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Enum;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Schema;

/// <summary>
/// The propety schema
/// </summary>
[Schema($"{NS_SYSTEM_SCHEMA_DEF_PROPERTY}.schema")]
public sealed class PropertySchema: ISchemaExtensions
{
    /// <summary>
    /// The property namespace
    /// </summary>
    [Index]
    [JsonIgnore]
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? Name { get; set; }

    /// <summary>
    /// The property name, such as "uplimit", "lowlimit", "pattern", etc.
    /// </summary>
    public string Property { get; internal set; } = string.Empty;

    /// <summary>
    /// The value type, null means use the target node type
    /// </summary>
    public string? ValueType { get; internal set; }

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
    public SchemaType[] ForSchemas { get; set; } = [];

    /// <summary>
    /// For value kinds
    /// </summary>
    public ValueSchemaType[]? ForValues { get; set; }

    /// <summary>
    /// Include the value type array
    /// </summary>
    public bool? IncludeArray { get; set; }

    /// <summary>
    /// The extensions
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extensions { get; set; }
}
