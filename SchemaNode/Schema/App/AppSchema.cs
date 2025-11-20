using SchemaNode.Attribute;
using SchemaNode.Enum;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Schema;

/**
 * The application schema
 */
[SchemaApp]
public class AppSchema
{
    /// <summary>
    /// The parent app name
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    [Index("IX_SUB_APP")]
    public string Parent { get; set; } = string.Empty;
    
    /// <summary>
    /// The application name
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    [Index]
    [Index("IX_SUB_APP")]
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
    /// The authentication policy type
    /// </summary>
    [Schema(NS_SYSTEM_SCHEMA_POLICY_TYPE)]
    public string? Auth { get; set; }

    /// <summary>
    /// The app authentication policy type
    /// </summary>
    public PolicyItem[]? Auths { get; set; }
    
    /// <summary>
    /// Whether it has sub-applications
    /// </summary>
    [NotMapped]
    public bool? HasApps { get; set; }
    
    /// <summary>
    /// Whether it has fields
    /// </summary>
    [NotMapped]
    public bool? HasFields { get; set; }

    /// <summary>
    /// The sub applications
    /// </summary>
    [NotMapped]
    public AppSchema[]? Apps { get; set; }
    
    /// <summary>
    /// The application fields
    /// </summary>
    [NotMapped]
    public AppFieldSchema[]? Fields { get; set; }
    
    /// <summary>
    /// The application workflows
    /// </summary>
    [NotMapped]
    public AppWorkflowSchema[]? Workflows { get; set; }
    
    /// <summary>
    /// The application field relations
    /// </summary>
    public StructFieldRelation[]? Relations { get; set; }
    
    /// <summary>
    /// The types related to the application
    /// </summary>
    [NotMapped]
    public NodeSchema[]? NodeSchemas { get; set; }

    /// <summary>
    /// The additional data
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
    
    /// <summary>
    /// The load state
    /// </summary>
    [NotMapped]
    public SchemaLoadState? LoadState { get; set; }
}

/// <summary>
/// The application ref
/// </summary>
public class AppRef
{
    /// <summary>
    /// The source app
    /// </summary>
    [Index]
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string App { get; set; } = string.Empty;

    /// <summary>
    /// The source target
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? Target { get; set; }
}