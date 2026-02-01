using System.Text.Json;
using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using System.ComponentModel.DataAnnotations;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Schema;

/// <summary>
/// The schema of the recognizer type
/// </summary>
public class RecognizerSchema
{
    /// <summary>
    /// The recognizer name
    /// </summary>
    [Index]
    [JsonIgnore]
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? Name { get; set; }

    
}