using SchemaNode.Attribute;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Schema;

/// <summary>
/// The event schema
/// </summary>
[SchemaApp]
[Schema($"{NS_SYSTEM_SCHEMA_DEF_EVENT}.schema")]
public sealed class EventSchema: ISchemaExtensions
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

    /// <summary>
    /// The extensions
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extensions { get; set; }
}