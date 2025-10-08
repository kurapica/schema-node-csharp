using SchemaNode.Enum;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SchemaNode.Schema;

/**
 * The application schema
 */
public class AppSchema
{
    /// <summary>
    /// The application name
    /// </summary>
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
    /// Standalone app without app target
    /// </summary>
    public bool? Standalone { get; set; }
    
    /// <summary>
    /// Whether it has sub-applications
    /// </summary>
    public bool? HasApps { get; set; }
    
    /// <summary>
    /// Whether it has fields
    /// </summary>
    public bool? HasFields { get; set; }

    /// <summary>
    /// The sub applications
    /// </summary>
    public AppSchema[]? Apps { get; set; }
    
    /// <summary>
    /// The application fields
    /// </summary>
    public AppFieldSchema[]? Fields { get; set; }
    
    /// <summary>
    /// The application field relations
    /// </summary>
    public StructFieldRelation[]? Relations { get; set; }
    
    /// <summary>
    /// The types related to the application
    /// </summary>
    public NodeSchema[]? NodeSchemas { get; set; }

    /// <summary>
    /// The additional data
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
}

/// <summary>
/// The application field schema
/// </summary>
public class AppFieldSchema
{
    /// <summary>
    /// The field name
    /// </summary>
    public string Name { get; set; } = default!;
    
    /// <summary>
    /// The field type
    /// </summary>
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
    public string? SourceApp { get; set; }
    
    /// <summary>
    /// The source field
    /// </summary>
    public string? SourceField { get; set; }
    
    /// <summary>
    /// The calculate function
    /// </summary>
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