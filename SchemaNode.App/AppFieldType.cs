using System.Text.Json;
using System.Text.Json.Serialization;
using SchemaNode.App.Schema;
using SchemaNode.Enum;
using SchemaNode.Schema;
using SchemaNode.Struct;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.App;

/// <summary>
/// The in-memory application field representation.
/// Populated from <see cref="AppFieldSchema"/> during application loading.
/// </summary>
public sealed class AppFieldType
{
    #region Properties

    /// <summary>Application name (set by the loader)</summary>
    public string App { get; internal set; } = string.Empty;

    /// <summary>Field name</summary>
    public required string Name { get; init; }

    /// <summary>Schema type name</summary>
    public string Type { get; init; } = null!;

    /// <summary>Display order</summary>
    public int Seqno { get; private init; }

    /// <summary>Display name</summary>
    public LocaleString? Display { get; init; }

    /// <summary>Push / calculate function name</summary>
    public string? Func { get; init; }

    /// <summary>Input source field for the push function</summary>
    public string? Arg { get; init; }

    /// <summary>Inline data-access policies</summary>
    public PolicyItem[]? Auths { get; private init; }

    /// <summary>Row-level filter policies</summary>
    public RowPolicy[]? RowAuths { get; private init; }

    /// <summary>Column-level access policies</summary>
    public ColPolicy[]? ColAuths { get; private init; }

    /// <summary>Foreign key settings</summary>
    public Foreign[]? Foreigns { get; private init; }

    /// <summary>View-from-source settings</summary>
    public FieldView? View { get; private init; }

    /// <summary>Storage topology</summary>
    public FieldStorageTopology? Topology { get; set; }

    /// <summary>Override table name</summary>
    public string? TableName { get; set; }

    /// <summary>Override attribute table name</summary>
    public string? AttrTableName { get; set; }

    /// <summary>Allow clearing all data for this field</summary>
    public bool? AllowClear { get; private init; }

    /// <summary>Only incremental updates are allowed</summary>
    public bool? IncrUpdate { get; private init; }

    /// <summary>Front-end only — no persistent storage</summary>
    public bool? Frontend { get; private init; }

    /// <summary>Field is disabled</summary>
    public bool? Disable { get; private init; }

    /// <summary>Read-only — data originates from another app</summary>
    public bool? Readonly { get; private init; }

    /// <summary>Dynamic table is maintained by the system</summary>
    public bool? SystemMaintain { get; set; }

    /// <summary>Combine rule for scalar / enum type</summary>
    public DataCombineType? Combine { get; private init; }

    /// <summary>Combine rules per sub-field for struct / array types</summary>
    public DataCombine[]? Combines { get; private init; }

    /// <summary>Data filters for this field</summary>
    public FieldFilter[]? Filters { get; private init; }

    /// <summary>Properties (resolved from Extensions at load time)</summary>
    public object[]? Properties { get; internal set; }

    /// <summary>Extension data</summary>
    public Dictionary<string, JsonElement>? Extensions { get; private init; }

    #endregion

    #region State

    /// <summary>Load status — null means ready</summary>
    public string? Status { get; internal set; }

    /// <summary>Whether the field creates a dynamic table (not frontend-only or disabled)</summary>
    public bool EnableDynamicTable => !(Frontend ?? false) && !(Disable ?? false);

    /// <summary>Whether this field is a read-only view of another app's field</summary>
    public bool IsForeignView => !string.IsNullOrWhiteSpace(View?.App);

    /// <summary>Whether any observer is registered</summary>
    public bool HasObserver => _observers is { Count: > 0 };

    #endregion

    #region Relationships

    /// <summary>Owning AppType</summary>
    public AppType Application { get; internal set; } = null!;

    /// <summary>Resolved schema type for the field data</summary>
    public Runtime.NodeType? SchemaType { get; internal set; }

    /// <summary>Resolved push-function node</summary>
    public Runtime.FunctionType? FuncNode { get; internal set; }

    /// <summary>Fields that observe this field for data push triggers</summary>
    public IReadOnlyList<AppFieldType>? Observers => _observers;

    private List<AppFieldType>? _observers;

    /// <summary>Source field driving data push into this field</summary>
    public AppFieldType? PushSource { get; internal set; }

    #endregion

    #region Methods

    /// <summary>Registers an observer field on this field.</summary>
    public void AddObserver(AppFieldType observer)
    {
        _observers ??= [];
        if (!_observers.Contains(observer))
            _observers.Add(observer);
    }

    /// <summary>
    /// Gets all effective auth policies for the given scope, inheriting from the owning application.
    /// </summary>
    public IEnumerable<PolicyItem> GetAuthPolicies(PolicyScope scope)
    {
        foreach (PolicyItem i in Application.GetAuthPolicies(scope))
            yield return i;

        if (Auths == null) yield break;
        foreach (PolicyItem i in Auths.Where(p => p.Scope == scope))
            yield return i;
    }

    /// <summary>Gets the evaluator function names for the given struct field column policy.</summary>
    public IEnumerable<string> GetColPolicies(string fieldName)
    {
        ColPolicy? item = ColAuths?.FirstOrDefault(i =>
            i.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
        if (item == null || item.Evaluators.Length == 0) yield break;
        foreach (string ev in item.Evaluators)
            yield return ev;
    }

    #endregion

    #region Conversions

    public static implicit operator AppFieldType(AppFieldSchema schema)
        => new()
        {
            Name = schema.Name,
            Type = schema.Type,
            Seqno = schema.Seqno,
            Display = schema.Display,
            Topology = schema.Topology,
            TableName = schema.TableName,
            AttrTableName = schema.AttrTableName,
            AllowClear = schema.AllowClear,
            Func = schema.Func,
            Arg = schema.Arg,
            Auths = schema.Auths,
            RowAuths = schema.RowAuths,
            ColAuths = schema.ColAuths,
            Foreigns = schema.Foreigns,
            View = schema.View,
            IncrUpdate = schema.IncrUpdate,
            Frontend = schema.Frontend,
            Disable = schema.Disable,
            Readonly = schema.Readonly,
            SystemMaintain = schema.SystemMaintain,
            Combine = schema.Combine,
            Combines = schema.Combines,
            Filters = schema.Filters,
            Extensions = schema.Extensions,
        };

    public static implicit operator AppFieldSchema(AppFieldType type)
        => new()
        {
            App = type.App,
            Name = type.Name,
            Type = type.Type,
            Seqno = type.Seqno,
            Display = type.Display,
            Topology = type.Topology,
            TableName = type.TableName,
            AttrTableName = type.AttrTableName,
            AllowClear = type.AllowClear,
            Func = type.Func,
            Arg = type.Arg,
            Auths = type.Auths,
            RowAuths = type.RowAuths,
            ColAuths = type.ColAuths,
            Foreigns = type.Foreigns,
            View = type.View,
            IncrUpdate = type.IncrUpdate,
            Frontend = type.Frontend,
            Disable = type.Disable,
            Readonly = type.Readonly,
            SystemMaintain = type.SystemMaintain,
            Combine = type.Combine,
            Combines = type.Combines,
            Filters = type.Filters,
            Extensions = type.Extensions,
        };

    #endregion
}
