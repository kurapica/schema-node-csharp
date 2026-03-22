using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Enum;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Schema;

/// <summary>
/// The presentation schema
/// </summary>
[Schema($"{NS_SYSTEM_SCHEMA_DEF_PRESENTATION}.schema")]
public sealed class PresentationSchema
{
    /// <summary>
    /// The presentation name
    /// </summary>
    [Index]
    [JsonIgnore]
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? Name { get; set; }


    /// <summary>
    /// The constraint property name, such as "visible", "desc" etc.
    /// </summary>
    public string Property { get; internal set; } = string.Empty;

    /// <summary>
    /// The presentation value type
    /// </summary>
    public string? ValueType { get; set; }

    /// <summary>
    /// The schema types that this presentation applies to
    /// </summary>
    public SchemaType[]? For { get; set; }
}
