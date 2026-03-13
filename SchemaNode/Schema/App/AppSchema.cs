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
public sealed class AppSchema
{
    #region Info

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

    #endregion

    #region Scope Policy

    /// <summary>
    /// The target policies, can only be changeable when no app & no fields or in debug mode
    /// </summary>
    public AppScopePolicy? ScopePolicy { get; set; }

    #endregion

    #region Auth Policy

    /// <summary>
    /// The authentication policy type
    /// </summary>
    [Schema(NS_SYSTEM_SCHEMA_POLICY_TYPE)]
    public string? Auth { get; set; }

    /// <summary>
    /// The app authentication policy type
    /// </summary>
    public PolicyItem[]? Auths { get; set; }

    #endregion

    #region Details

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
    public StructRelationSchema[]? Relations { get; set; }
    
    /// <summary>
    /// The types related to the application
    /// </summary>
    [NotMapped]
    public NodeSchema[]? NodeSchemas { get; set; }

    #endregion

    #region Status

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
    
    /// <summary>
    /// The schema node status
    /// </summary>
    [NotMapped]
    public SchemaNodeStatus? Status { get; set; }

    #endregion

    #region Methd

    /// <summary>
    /// Combine custom app schema
    /// </summary>
    internal void CombineCustomSchema(AppSchema? otherSchema)
    {
        if (otherSchema == null) return;
        Display = Display != null ? Display.Concat(otherSchema.Display) : otherSchema.Display;
        Desc = Desc != null ? Desc.Concat(otherSchema.Desc) : otherSchema.Desc;
        Auth = string.IsNullOrWhiteSpace(Auth) ? otherSchema.Auth : Auth;
        Auths = Auths ?? otherSchema.Auths;

        // Check fields
        if (HasApps != true)
        {
            if (Fields == null || Fields.Length == 0)
                Fields = otherSchema.Fields;
            else if(otherSchema.Fields is { Length: > 0 })
            {
                foreach(var field in Fields)
                {
                    field.CombineCustomSchema(otherSchema.Fields.FirstOrDefault(f => f.Name.Equals(field.Name, StringComparison.OrdinalIgnoreCase)));
                }
                var addFields = otherSchema.Fields.Where(f => !Fields.Any(d => d.Name.Equals(f.Name, StringComparison.OrdinalIgnoreCase))).ToArray();
                if (addFields.Length > 0)
                    Fields = Fields.Concat(addFields).ToArray();
            }
        }

        // For simple
        Workflows = otherSchema.Workflows ?? Workflows;
        Relations = otherSchema.Relations ?? Relations;
    }

    #endregion
}

/// <summary>
/// The app target policy
/// </summary>
public sealed class AppScopePolicy: IEquatable<AppScopePolicy>
{
    /// <summary>
    /// The app target policy type
    /// </summary>
    public AppScopeType Type { get; set; }
    
    /// <summary>
    /// The context maps for the context item mapping when the target policy is IsolationContext, can be used for multiple context items mapping
    /// </summary>
    public AppScopeContextMap[]? ContextMaps { get; set; }
    
    public bool Equals(AppScopePolicy? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Type == other.Type && 
               ((ContextMaps == null && other.ContextMaps == null) || 
                (ContextMaps != null && other.ContextMaps != null && 
                 ContextMaps.SequenceEqual(other.ContextMaps)));
    }
}

/// <summary>
/// The application scope context map, used for the context item mapping when the target policy is IsolationContext
/// </summary>
public sealed class AppScopeContextMap: IEquatable<AppScopeContextMap>
{
    /// <summary>
    /// The context item
    /// </summary>
    public required string ContextItem { get; set; }
    
    /// <summary>
    /// The map key
    /// </summary>
    public string? MapKey { get; set; }

    public bool Equals(AppScopeContextMap? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return ContextItem.Equals(other.ContextItem) &&
               (string.IsNullOrWhiteSpace(MapKey) 
                   ? string.IsNullOrWhiteSpace(other.MapKey)
                   : MapKey.Equals(other.MapKey));
    }
}

