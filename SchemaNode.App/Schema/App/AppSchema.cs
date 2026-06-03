using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Scalar;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;
using SchemaKind = SchemaNode.Property.Record.SchemaKind;
using String = SchemaNode.Scalar.String;
using SchemaNode.Function;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Schema;

/**
 * The application schema
 */
[Meta<SchemaKind>(SCHEMA_KIND_APP, SCHEMA_KIND_ORDER_APP)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP}.schema")]
[Meta<Append>(typeof(Display), typeof(Relations))]
public sealed class AppSchema: ExtensibleSchema
{
    /// <summary>
    /// The application name
    /// </summary>
    [Meta<PrimaryIndex>(1)]
    [Meta<SchemaType>(typeof(Identifier))]
    public string Name { get; set; } = default!;

    /// <summary>
    /// The parent app name
    /// </summary>
    [Meta<PrimaryIndex>(0)]
    [Meta<SchemaType>(typeof(AppType))]
    public string? Parent { get; set; }
    
    /// <summary>
    /// The full name of the app
    /// </summary>
    [SchemaIgnore]
    [JsonIgnore]
    public string FullName => $"{Parent}.{Name}".Trim('.');

    /// <summary>
    /// The target policies, can only be changeable when no app & no fields or in debug mode
    /// </summary>
    public AppScopePolicy? ScopePolicy { get; set; }

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
    public RelationSchema[]? Relations { get; set; }
    
    /// <summary>
    /// The types related to the application
    /// </summary>
    [NotMapped]
    public NodeSchema[]? NodeSchemas { get; set; }

    #endregion

    #region Status

    /// <summary>
    /// The load state
    /// </summary>
    [NotMapped]
    public SchemaLoadState? LoadState { get; set; }
    
    #endregion
}

/// <summary>
/// The application type, used for the parent app reference and app type definition, it's a string with format of {appnamespace}.{appname}
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP}.type")]
[Meta<EntrySource>($"{NS_SYSTEM_SCHEMA_REFLECT}.{nameof(SystemAppReflect.getapps)}", NODE_SELF)]
public sealed class AppType : String;

/// <summary>
/// The app target policy
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP}.ScopePolicy")]
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
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP}.ScopeContextMap")]
public sealed class AppScopeContextMap: IEquatable<AppScopeContextMap>
{
    /// <summary>
    /// The context item
    /// </summary>
    public required string ContextItem { get; set; }

    /// <summary>
    /// The map key
    /// </summary>
    [Meta<SchemaType>(typeof(Identifier))]
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

