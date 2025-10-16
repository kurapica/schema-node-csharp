using SchemaNode.Enum;
using System.Text.Json;
using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using static SchemaNode.Utility.Constant;
using System.ComponentModel.DataAnnotations;

namespace SchemaNode.Schema;

/**
 * The application schema
 */
[SchemaStruct([nameof(Name)], [nameof(Parent), nameof(Name)])]
[SchemaApp]
public class AppSchema
{
    /// <summary>
    /// The parent app name
    /// </summary>
    [MaxLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string Parent { get; set; } = string.Empty;
    
    /// <summary>
    /// The application name
    /// </summary>
    [MaxLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string Name { get; set; } = default!;
    
    /// <summary>
    /// The display name
    /// </summary>
    public LocaleString? Display { get; set; }
    
    /// <summary>
    /// The description
    /// </summary>
    public LocaleString? Desc { get; set; }
    
    /// <summary>
    /// Whether it has sub-applications
    /// </summary>
    [SchemaStructMemIgnore]
    public bool? HasApps { get; set; }
    
    /// <summary>
    /// Whether it has fields
    /// </summary>
    [SchemaStructMemIgnore]
    public bool? HasFields { get; set; }

    /// <summary>
    /// The sub applications
    /// </summary>
    [SchemaStructMemIgnore]
    public AppSchema[]? Apps { get; set; }
    
    /// <summary>
    /// The application fields
    /// </summary>
    [SchemaStructMemIgnore]
    public AppFieldSchema[]? Fields { get; set; }
    
    /// <summary>
    /// The application field relations
    /// </summary>
    public StructFieldRelation[]? Relations { get; set; }
    
    /// <summary>
    /// The types related to the application
    /// </summary>
    [SchemaStructMemIgnore]
    public NodeSchema[]? NodeSchemas { get; set; }

    /// <summary>
    /// The additional data
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
    
    /// <summary>
    /// The load state
    /// </summary>
    [SchemaStructMemIgnore]
    public SchemaLoadState? LoadState { get; set; }
}

/// <summary>
/// The application field schema
/// </summary>
[SchemaStruct([nameof(App), nameof(Name)])]
[SchemaApp]
public class AppFieldSchema
{
    /// <summary>
    /// the application name
    /// </summary>
    [MaxLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string App { get; set; } = string.Empty;
    
    /// <summary>
    /// The seqno
    /// </summary>
    public int Seqno { get; set; } = 0;
    
    /// <summary>
    /// The field name
    /// </summary>
    [MaxLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string Name { get; set; } = default!;
    
    /// <summary>
    /// The field type
    /// </summary>
    [MaxLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string Type { get; set; } = default!;
    
    /// <summary>
    /// The field display name
    /// </summary>
    public LocaleString? Display { get; set; }
    
    /// <summary>
    /// The field description
    /// </summary>
    public LocaleString? Desc { get; set; }
    
    /// <summary>
    /// The source application
    /// </summary>
    [MaxLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? SourceApp { get; set; }
    
    /// <summary>
    /// The source field
    /// </summary>
    [MaxLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? SourceField { get; set; }
    
    /// <summary>
    /// Track the push data to the source field, so toggle the source target, will also re-push the data
    /// </summary>
    public bool? TrackPush { get; set; }
    
    /// <summary>
    /// The calculate function
    /// </summary>
    [MaxLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? Func { get; set; }
    
    /// <summary>
    /// The input fields
    /// </summary>
    public string[]? Args { get; set; }
    
    /// <summary>
    /// The field is using increase update, no full data push allowed
    /// </summary>
    public bool? IncrUpdate { get; set; }
    
    /// <summary>
    /// The field is front-end only, no data storage
    /// </summary>
    public bool? Frontend { get; set; }
    
    /// <summary>
    /// The field is disabled
    /// </summary>
    public bool? Disable  { get; set; }
    
    /// <summary>
    /// The field is readonly, data comes from other apps
    /// </summary>
    public bool? Readonly { get; set; }
    
    /// <summary>
    /// The combine rule for scalar/enum type
    /// </summary>
    public DataCombineType? Combine { get; set; }
    
    /// <summary>
    /// The combine rule for struct or struct-array type
    /// </summary>
    public DataCombine[]? Combines { get; set; }

    /// <summary>
    /// The additional data
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
}

/// <summary>
/// The application ref
/// </summary>
[SchemaStruct([nameof(App)])]
public class AppRef
{
    /// <summary>
    /// The source app
    /// </summary>
    [MaxLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string App { get; set; } = string.Empty;

    /// <summary>
    /// The source target
    /// </summary>
    [MaxLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? Target { get; set; }
}