using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using SchemaNode.Enum;
using SchemaNode.Schema;
using SchemaNode.Struct;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.App.Schema;

/// <summary>
/// The application field schema — describes one data field belonging to an application.
/// </summary>
public sealed class AppFieldSchema
{
    #region Identity

    /// <summary>Application name this field belongs to</summary>
    public string App { get; set; } = string.Empty;

    /// <summary>Field name</summary>
    public string Name { get; set; } = default!;

    /// <summary>Display order</summary>
    public int Seqno { get; set; }

    /// <summary>Schema type name of the field data</summary>
    public string Type { get; set; } = default!;

    /// <summary>Display name</summary>
    public LocaleString? Display { get; set; }

    /// <summary>Extension data</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extensions { get; set; }

    #endregion

    #region Push Rule

    /// <summary>Push / calculate function name</summary>
    public string? Func { get; set; }

    /// <summary>Input source field for the push function</summary>
    public string? Arg { get; set; }

    #endregion

    #region Auth

    /// <summary>Inline data-access policy items</summary>
    public PolicyItem[]? Auths { get; set; }

    /// <summary>Row-level filter policies</summary>
    public RowPolicy[]? RowAuths { get; set; }

    /// <summary>Column-level access policies</summary>
    public ColPolicy[]? ColAuths { get; set; }

    #endregion

    #region Storage

    /// <summary>Storage topology for this field</summary>
    public FieldStorageTopology? Topology { get; set; }

    /// <summary>Override table name (auto-generated if null)</summary>
    public string? TableName { get; set; }

    /// <summary>Override attribute table name (auto-generated if null)</summary>
    public string? AttrTableName { get; set; }

    #endregion

    #region Flags

    /// <summary>All field flags packed together</summary>
    [JsonIgnore]
    public AppFieldFlags Flags { get; set; } = AppFieldFlags.None;

    /// <summary>Front-end only — no persistent storage</summary>
    public bool? Frontend
    {
        get => Flags.Has(AppFieldFlags.Frontend);
        init => Flags = Flags.Turn(AppFieldFlags.Frontend, value);
    }

    /// <summary>Field is disabled</summary>
    public bool? Disable
    {
        get => Flags.Has(AppFieldFlags.Disable);
        init => Flags = Flags.Turn(AppFieldFlags.Disable, value);
    }

    /// <summary>Read-only — data originates from another app</summary>
    public bool? Readonly
    {
        get => Flags.Has(AppFieldFlags.Readonly);
        init => Flags = Flags.Turn(AppFieldFlags.Readonly, value);
    }

    /// <summary>Only incremental updates are allowed; no full pushes</summary>
    public bool? IncrUpdate
    {
        get => Flags.Has(AppFieldFlags.IncrUpdate);
        init => Flags = Flags.Turn(AppFieldFlags.IncrUpdate, value);
    }

    /// <summary>Allow clearing all data for this field</summary>
    public bool? AllowClear
    {
        get => Flags.Has(AppFieldFlags.AllowClear);
        init => Flags = Flags.Turn(AppFieldFlags.AllowClear, value);
    }

    /// <summary>Dynamic table is maintained by the system (not by schema migration)</summary>
    public bool? SystemMaintain { get; set; }

    #endregion

    #region Combine Rules

    /// <summary>Combine rule for scalar / enum type</summary>
    public DataCombineType? Combine { get; set; }

    /// <summary>Combine rules per sub-field for struct/array types</summary>
    public DataCombine[]? Combines { get; set; }

    #endregion

    #region Filters

    /// <summary>Data filters for this field</summary>
    public FieldFilter[]? Filters { get; set; }

    #endregion

    #region Foreign & View

    /// <summary>Foreign key / reference settings</summary>
    public Foreign[]? Foreigns { get; set; }

    /// <summary>View-from-source settings</summary>
    public FieldView? View { get; set; }

    #endregion

    #region Methods

    /// <summary>
    /// Merges a custom (provider-supplied) field schema into this base definition.
    /// </summary>
    internal void CombineCustomSchema(AppFieldSchema? other)
    {
        if (other == null) return;
        Display = Display is not null ? Display.Concat(other.Display) : other.Display;
        Auths ??= other.Auths;
        RowAuths ??= other.RowAuths;
        ColAuths ??= other.ColAuths;
        Flags |= other.Flags;
        Combine ??= other.Combine;
        Combines ??= other.Combines;
        Filters ??= other.Filters;
        Foreigns ??= other.Foreigns;
        View ??= other.View;
    }

    #endregion
}

// ── Supporting Types ──────────────────────────────────────────────────────────

/// <summary>Field-level storage and behaviour flags.</summary>
[Flags]
public enum AppFieldFlags
{
    None       = 0,
    Frontend   = 1 << 0,
    Disable    = 1 << 1,
    Readonly   = 1 << 2,
    IncrUpdate = 1 << 3,
    AllowClear = 1 << 4,
}

/// <summary>
/// Helper to set / test individual <see cref="AppFieldFlags"/> bits.
/// Kept internal to avoid polluting the public surface.
/// </summary>
internal static class AppFieldFlagsExtensions
{
    public static bool Has(this AppFieldFlags flags, AppFieldFlags flag)
        => flag != AppFieldFlags.None && (flags & flag) == flag;

    public static AppFieldFlags Turn(this AppFieldFlags flags, AppFieldFlags flag, bool? value)
        => value == true ? flags | flag : value == false ? flags & ~flag : flags;
}

/// <summary>Per-field combine rule for struct / array fields.</summary>
public sealed class DataCombine
{
    /// <summary>The sub-field name</summary>
    [MaxLength(128)]
    public string Field { get; set; } = string.Empty;

    /// <summary>The combine type for this sub-field</summary>
    public DataCombineType Type { get; set; } = DataCombineType.Assign;
}

/// <summary>Row-level (filter) policy.</summary>
public sealed class RowPolicy
{
    /// <summary>Evaluator function name — when true the row filter is applied</summary>
    public required string Evaluator { get; set; }

    /// <summary>Filter function name — provides the actual row filter</summary>
    public string? Filter { get; set; }

    /// <summary>Resolved evaluator function (set at runtime)</summary>
    [JsonIgnore]
    public Runtime.FunctionType? EvaluatorFunc { get; set; }

    /// <summary>Resolved filter function (set at runtime)</summary>
    [JsonIgnore]
    public Runtime.FunctionType? FilterFunc { get; set; }
}

/// <summary>Column-level access policy.</summary>
public sealed class ColPolicy
{
    /// <summary>Struct field name</summary>
    public required string Name { get; set; }

    /// <summary>Evaluator function names</summary>
    public string[] Evaluators { get; set; } = [];

    /// <summary>Resolved function nodes (set at runtime)</summary>
    [JsonIgnore]
    public Runtime.FunctionType[] Functions { get; set; } = [];
}

/// <summary>Data filter applied to field reads.</summary>
public sealed class FieldFilter
{
    /// <summary>Filter mode</summary>
    public FieldFilterMode Mode { get; set; } = FieldFilterMode.Exactly;

    /// <summary>Field name (for non-Filter modes) or filter function name (for Filter mode)</summary>
    [MaxLength(128)]
    public string Filter { get; set; } = string.Empty;

    /// <summary>Resolve strategy when no matching value is found</summary>
    public FieldFilterResolve? Resolve { get; set; }
}

/// <summary>Foreign key / reference to another application field.</summary>
public sealed class Foreign
{
    /// <summary>The struct field that holds the foreign key</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>Target application name</summary>
    public string App { get; set; } = string.Empty;
}

/// <summary>View-from-source definition — makes this field a read-only projection of another app's field.</summary>
public sealed class FieldView
{
    /// <summary>Source application name</summary>
    public string App { get; set; } = string.Empty;

    /// <summary>Source field name</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>Target struct field that serves as the join key</summary>
    public string Map { get; set; } = string.Empty;

    /// <summary>Resolved source AppType (set at runtime)</summary>
    [JsonIgnore]
    public AppType? AppType { get; set; }
}
