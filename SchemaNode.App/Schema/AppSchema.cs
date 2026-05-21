using System.Text.Json;
using System.Text.Json.Serialization;
using SchemaNode.Enum;
using SchemaNode.Schema;
using SchemaNode.Struct;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.App.Schema;

/// <summary>
/// The application schema — describes an application node with fields, workflows, and relations.
/// This is a database-resident entity, loaded through <see cref="Service.IAppSchemaProvider"/>.
/// It is intentionally NOT an ExtensibleSchema because it is the root of the App schema family,
/// not a schema kind registered in the Core NodeSchema extension system.
/// </summary>
public sealed class AppSchema
{
    #region Identity

    /// <summary>Parent application name (empty for root applications)</summary>
    public string Parent { get; set; } = string.Empty;

    /// <summary>Application name</summary>
    public string Name { get; set; } = default!;

    /// <summary>Display name</summary>
    public LocaleString? Display { get; set; }

    /// <summary>Extension data</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extensions { get; set; }

    #endregion

    #region Scope Policy

    /// <summary>The scope / isolation policy for this application</summary>
    public AppScopePolicy? ScopePolicy { get; set; }

    #endregion

    #region Auth Policy

    /// <summary>Reference to a PolicySchema by name</summary>
    public string? Auth { get; set; }

    /// <summary>Inline authentication policy items</summary>
    public PolicyItem[]? Auths { get; set; }

    #endregion

    #region Details

    /// <summary>Whether this app has sub-applications (populated at runtime)</summary>
    [JsonIgnore]
    public bool? HasApps { get; set; }

    /// <summary>Whether this app has fields (populated at runtime)</summary>
    [JsonIgnore]
    public bool? HasFields { get; set; }

    /// <summary>Sub-applications (populated at runtime)</summary>
    [JsonIgnore]
    public AppSchema[]? Apps { get; set; }

    /// <summary>Application fields</summary>
    [JsonIgnore]
    public AppFieldSchema[]? Fields { get; set; }

    /// <summary>Application workflows</summary>
    [JsonIgnore]
    public AppWorkflowSchema[]? Workflows { get; set; }

    /// <summary>App-level relation rules (stored in the schema)</summary>
    public StructRelationSchema[]? Relations { get; set; }

    #endregion

    #region Methods

    /// <summary>
    /// Merges a custom (provider-supplied) schema into this base schema.
    /// Fields present in both are merged; missing fields from the other are appended.
    /// </summary>
    internal void CombineCustomSchema(AppSchema? other)
    {
        if (other == null) return;

        Display = Display is not null ? Display.Concat(other.Display) : other.Display;
        Auth = string.IsNullOrWhiteSpace(Auth) ? other.Auth : Auth;
        Auths ??= other.Auths;

        // Fields
        if (HasApps != true)
        {
            if (Fields == null || Fields.Length == 0)
            {
                Fields = other.Fields;
            }
            else if (other.Fields is { Length: > 0 })
            {
                foreach (AppFieldSchema field in Fields)
                    field.CombineCustomSchema(other.Fields.FirstOrDefault(f =>
                        f.Name.Equals(field.Name, StringComparison.OrdinalIgnoreCase)));

                AppFieldSchema[] addFields = other.Fields
                    .Where(f => !Fields.Any(d => d.Name.Equals(f.Name, StringComparison.OrdinalIgnoreCase)))
                    .ToArray();
                if (addFields.Length > 0)
                    Fields = [..Fields, ..addFields];
            }
        }

        Workflows = other.Workflows ?? Workflows;
        Relations = other.Relations ?? Relations;
    }

    #endregion
}

/// <summary>The scope/isolation policy for an application.</summary>
public sealed class AppScopePolicy : IEquatable<AppScopePolicy>
{
    /// <summary>The isolation type</summary>
    public AppScopeType Type { get; set; }

    /// <summary>Context item mappings for IsolationContext type</summary>
    public AppScopeContextMap[]? ContextMaps { get; set; }

    public bool Equals(AppScopePolicy? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Type == other.Type &&
               ((ContextMaps == null && other.ContextMaps == null) ||
                (ContextMaps != null && other.ContextMaps != null && ContextMaps.SequenceEqual(other.ContextMaps)));
    }
}

/// <summary>Maps a context item to a scope key.</summary>
public sealed class AppScopeContextMap : IEquatable<AppScopeContextMap>
{
    /// <summary>The context item path (e.g. "Access.Target")</summary>
    public required string ContextItem { get; set; }

    /// <summary>Optional override key; defaults to "_&lt;lastSegment&gt;"</summary>
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
