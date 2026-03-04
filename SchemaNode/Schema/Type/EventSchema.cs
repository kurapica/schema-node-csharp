using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Schema;

/// <summary>
/// The event schema
/// </summary>
[SchemaApp]
public sealed class EventSchema
{
    /// <summary>
    /// The event name
    /// </summary>
    [Index]
    [JsonIgnore]
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? Name { get; set; }
    
    /// <summary>
    /// The event value type
    /// </summary>
    public string? Payload { get; set; }
}