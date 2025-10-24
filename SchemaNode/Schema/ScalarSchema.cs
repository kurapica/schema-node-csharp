using System.Text.Json;
using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using System.ComponentModel.DataAnnotations;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Schema;

/**
 * The schema of the scalar type
*/
[SchemaApp]
public class ScalarSchema
{
    /// <summary>
    /// The scalar name
    /// </summary>
    [Index]
    [JsonIgnore]
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? Name { get; set; }

    /// <summary>
    /// The base type of the scalar
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? Base { get; set; }

    /// <summary>
    /// The default unit of the scalar value
    /// </summary>
    public LocaleString? Unit { get; set; }

    /// <summary>
    /// The default low limit of the scalar value
    /// </summary>
    public decimal? LowLimit { get; set; }

    /// <summary>
    /// The default up limit of the scalar value
    /// </summary>
    public decimal? UpLimit { get; set; }

    /// <summary>
    /// The default error message of the scalar value
    /// </summary>
    public LocaleString? Error  { get; set; }

    /// <summary>
    /// The regex of the scalar value
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? Regex  { get; set; }
    
    /// <summary>
    /// The white list function
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? WhiteList { get; set; }
    
    /// <summary>
    /// As suggest
    /// </summary>
    public bool? AsSuggest { get; set; }

    /// <summary>
    /// The function to validate the scalar value in frontend
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? PreValid  { get; set; }

    /// <summary>
    /// The eval function to convert the scalar value
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? PostValid  { get; set; }// 用来存放额外的字段
    
    /// <summary>
    /// The additional data
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
}