using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Enum;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Schema;

/// <summary>
/// The constraint schema
/// </summary>
[Schema($"{NS_SYSTEM_SCHEMA_DEF_CONSTRAINT}.schema")]
public sealed class ConstraintSchema
{
    /// <summary>
    /// The constraint name
    /// </summary>
    [Index]
    [JsonIgnore]
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? Name { get; set; }

    /// <summary>
    /// The constraint property name, such as "uplimit", "lowlimit", "pattern", etc.
    /// </summary>
    public string Property { get; internal set; } = string.Empty;

    /// <summary>
    /// The constraint value type
    /// </summary>
    public string? ValueType { get; set; }
    
    /// <summary>
    /// The required constraint names that this constraint depends on
    /// </summary>
    public string[]? Depends { get; set; }

    /// <summary>
    /// The optional constraint names that this constraint depends on
    /// </summary>
    public string[]? OptionDepends { get; set; }

    /// <summary>
    /// The schema types that this constraint applies to
    /// </summary>
    public SchemaType[]? For { get; set; }
}
