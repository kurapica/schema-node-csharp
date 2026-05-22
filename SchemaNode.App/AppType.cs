using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using SchemaNode.App.Schema;
using SchemaNode.Enum;
using SchemaNode.Schema;
using SchemaNode.Struct;
using static SchemaNode.App.AppStatus;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.App;

/// <summary>
/// The in-memory application representation.
/// Populated from <see cref="AppSchema"/> via <see cref="LoadAsync"/>.
/// </summary>
public sealed class AppType
{
    #region Properties

    /// <summary>Application name</summary>
    public required string Name { get; init; }

    /// <summary>Display name</summary>
    public LocaleString? Display { get; private set; }

    /// <summary>Scope / isolation policy</summary>
    public AppScopePolicy? ScopePolicy { get; private set; }

    /// <summary>Effective scope type</summary>
    public AppScopeType ScopeType => ScopePolicy?.Type ?? AppScopeType.BusinessTarget;

    /// <summary>Reference to a PolicySchema by name (resolved at load time)</summary>
    public string? AuthName { get; private set; }

    /// <summary>Resolved policy node (may be null if not specified)</summary>
    public Runtime.NodeType? Auth { get; private set; }

    /// <summary>Inline authentication policy items</summary>
    public PolicyItem[]? Auths { get; private set; }

    /// <summary>App-level relation rules</summary>
    public List<AppRelationSchema>? Relations { get; private set; }

    /// <summary>Sub-application schemas (populated lazily during preload)</summary>
    public AppSchema[]? Apps { get; internal set; }

    /// <summary>Application fields</summary>
    public List<AppFieldType>? Fields { get; set; }

    /// <summary>Application workflows</summary>
    public List<AppWorkflowType>? Workflows { get; set; }

    /// <summary>Sub-application map (key = sub-app name segment)</summary>
    public ConcurrentDictionary<string, AppType>? SubAppList { get; set; }

    /// <summary>Extension data</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extensions { get; internal set; }

    /// <summary>Root application (null for the root itself)</summary>
    public AppType? RootApp { get; init; }

    #endregion

    #region State

    /// <summary>
    /// Aggregate status — derived from field / auth / relation states.
    /// Returns a non-Ready code on the first problem found.
    /// </summary>
    public string Status =>
        Fields is { Count: > 0 } && Fields.Any(p => p.Status != null && p.Status != Ready)
            ? ApplicationInvalidField
            : Auths != null && Auths.Any(p => p.Status != null && p.Status != Ready)
                ? ApplicationDataAuthWrongFunc
                : Relations != null && Relations.Any(r => r.Status != null && r.Status != Ready)
                    ? ApplicationRelationWrongFunc
                    : Ready;

    /// <summary>Whether this app is in active use</summary>
    public bool IsUsed => Fields is { Count: > 0 } || Apps is { Length: > 0 };

    /// <summary>Prevents re-entrant loading</summary>
    internal bool Loaded { get; set; }

    #endregion

    #region Methods

    /// <summary>
    /// Loads (or reloads) the application from the supplied schema.
    /// Pass <paramref name="preLoad"/> = true to pre-fetch sub-application types without full validation.
    /// </summary>
    public async Task LoadAsync(IAppSchemaContext context, AppSchema schema, bool preLoad = false)
    {
        Release();

        Display = schema.Display;
        ScopePolicy = schema.ScopePolicy;
        AuthName = schema.Auth;
        Apps = schema.Apps;
        Extensions = schema.Extensions;

        // Resolve the named auth policy node
        Auth = !string.IsNullOrWhiteSpace(schema.Auth)
            ? await context.GetNodeTypeAsync(schema.Auth)
            : null;

        Auths = schema.Auths;

        // --- Fields ---
        Fields = schema.Fields?.Select(f => (AppFieldType)f).ToList();
        Relations = null;
        List<AppType>? reloadApps = null;

        if (Fields is { Count: > 0 })
        {
            // First pass: resolve field schema types
            foreach (AppFieldType field in Fields)
            {
                field.App = Name;
                field.Application = this;
                field.Status = null;

                Runtime.NodeType? node = await context.GetNodeTypeAsync(field.Type);
                if (node == null)
                    field.Status = ApplicationFieldWrongType;
                else
                {
                    node.AddUsedBy(field);
                    field.SchemaType = node;
                }
            }

            // Second pass: resolve push functions, auth, filters, foreign, view
            foreach (AppFieldType field in Fields)
            {
                // --- Push function ---
                if (!string.IsNullOrWhiteSpace(field.Func))
                {
                    Runtime.FunctionType? funcNode = await context.GetNodeTypeAsync(field.Func) as Runtime.FunctionType;
                    if (funcNode == null)
                    {
                        field.Status = ApplicationFieldWrongFunc;
                        continue;
                    }
                    funcNode.AddUsedBy(field);
                    field.FuncNode = funcNode;

                    if (!string.IsNullOrWhiteSpace(field.Arg))
                    {
                        AppFieldType? pushSource = GetField(field.Arg);
                        if (pushSource == null)
                        {
                            field.Status = ApplicationFieldWrongFuncField;
                        }
                        else
                        {
                            pushSource.AddObserver(field);
                            field.PushSource = pushSource;
                        }
                    }
                }

                // --- Data auth ---
                if (field.Auths != null)
                {
                    foreach (PolicyItem item in field.Auths)
                    {
                        Runtime.FunctionType? fn = !string.IsNullOrEmpty(item.Evaluator)
                            ? await context.GetNodeTypeAsync(item.Evaluator) as Runtime.FunctionType
                            : null;
                        if (fn != null)
                            item.Function = fn;
                        else
                            field.Status = ApplicationFieldDataAuthWrongFunc;
                    }
                }

                // --- Row policy ---
                if (field.RowAuths != null)
                {
                    foreach (RowPolicy row in field.RowAuths)
                    {
                        if (!string.IsNullOrEmpty(row.Evaluator))
                        {
                            Runtime.FunctionType? fn = await context.GetNodeTypeAsync(row.Evaluator) as Runtime.FunctionType;
                            if (fn != null) row.EvaluatorFunc = fn;
                            else field.Status = ApplicationFieldDataAuthWrongFunc;
                        }
                        if (!string.IsNullOrEmpty(row.Filter))
                        {
                            Runtime.FunctionType? fn = await context.GetNodeTypeAsync(row.Filter) as Runtime.FunctionType;
                            if (fn != null) row.FilterFunc = fn;
                            else field.Status = ApplicationFieldDataAuthWrongFunc;
                        }
                    }
                }

                // --- Column policy ---
                if (field.ColAuths != null)
                {
                    foreach (ColPolicy col in field.ColAuths)
                    {
                        List<Runtime.FunctionType> funcs = [];
                        foreach (string ev in col.Evaluators)
                        {
                            Runtime.FunctionType? fn = !string.IsNullOrEmpty(ev)
                                ? await context.GetNodeTypeAsync(ev) as Runtime.FunctionType
                                : null;
                            if (fn != null) funcs.Add(fn);
                            else field.Status = ApplicationFieldDataAuthWrongFunc;
                        }
                        col.Functions = [..funcs];
                    }
                }

                // --- Filters ---
                if (field.Filters is { Length: > 0 })
                {
                    foreach (FieldFilter filter in field.Filters)
                    {
                        if (filter.Mode == FieldFilterMode.Filter)
                        {
                            Runtime.NodeType? fn = await context.GetNodeTypeAsync(filter.Filter);
                            if (fn == null)
                            {
                                field.Status = ApplicationFieldDataWrongFilter;
                                break;
                            }
                        }
                    }
                }

                // --- Foreign key reference ---
                if (field.Foreigns is { Length: > 0 })
                {
                    foreach (Foreign foreign in field.Foreigns)
                    {
                        if (string.IsNullOrWhiteSpace(foreign.Field) ||
                            string.IsNullOrWhiteSpace(foreign.App) ||
                            await context.GetAppTypeAsync(foreign.App) is not AppType refApp ||
                            refApp.ScopeType == AppScopeType.SystemLevel)
                        {
                            field.Status = ApplicationFieldWrongRef;
                            break;
                        }
                        reloadApps ??= [];
                        reloadApps.Add(refApp);
                    }
                }

                // --- View ---
                if (!string.IsNullOrWhiteSpace(field.View?.App) || !string.IsNullOrWhiteSpace(field.View?.Field))
                {
                    if (string.IsNullOrWhiteSpace(field.View?.App) || string.IsNullOrWhiteSpace(field.View?.Field) ||
                        await context.GetAppTypeAsync(field.View.App) is not AppType sourceApp ||
                        sourceApp.ScopeType == AppScopeType.SystemLevel ||
                        sourceApp.GetField(field.View.Field) == null ||
                        sourceApp.GetField(field.View.Field)!.Foreigns == null ||
                        sourceApp.GetField(field.View.Field)!.Foreigns!.All(f =>
                            !f.App.Equals(Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        field.Status = ApplicationFieldWrongRef;
                    }
                    else
                    {
                        field.View!.AppType = sourceApp;
                    }
                }
            }

            // --- Relations ---
            if (schema.Relations is { Length: > 0 })
            {
                Relations = schema.Relations.Select(r => new AppRelationSchema
                {
                    AppField = r.Field.Split('.', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty,
                    DataField = r.Field.Contains('.') ? r.Field.Split('.', 2, StringSplitOptions.RemoveEmptyEntries)[1] : string.Empty,
                    Prop = r.Prop,
                    Func = r.Func,
                    Args = r.Args.Select(a => new AppArgSchema
                    {
                        AppField = a.Name?.Split('.', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty,
                        DataField = a.Name != null && a.Name.Contains('.')
                            ? a.Name.Split('.', 2, StringSplitOptions.RemoveEmptyEntries)[1]
                            : string.Empty,
                        Value = a.Value,
                    }).ToArray(),
                }).ToList();

                foreach (AppRelationSchema rel in Relations)
                {
                    AppFieldType? field = Fields.FirstOrDefault(f =>
                        f.Name.Equals(rel.AppField, StringComparison.OrdinalIgnoreCase));
                    if (field == null)
                    {
                        rel.Status = ApplicationRelationWrongTarget;
                        continue;
                    }
                    rel.FieldNode = field;

                    if (string.IsNullOrWhiteSpace(rel.Func))
                    {
                        rel.Status = ApplicationRelationWrongFunc;
                    }
                    else
                    {
                        Runtime.FunctionType? fn = await context.GetNodeTypeAsync(rel.Func) as Runtime.FunctionType;
                        if (fn != null)
                        {
                            fn.AddUsedBy(field);
                            rel.FuncNode = fn;
                        }
                        else
                        {
                            rel.Status = ApplicationRelationWrongFunc;
                        }
                    }
                }
            }
        }

        // --- App-level auth ---
        if (Auths != null)
        {
            foreach (PolicyItem item in Auths)
            {
                Runtime.FunctionType? fn = !string.IsNullOrEmpty(item.Evaluator)
                    ? await context.GetNodeTypeAsync(item.Evaluator) as Runtime.FunctionType
                    : null;
                if (fn != null)
                {
                    item.Function = fn;
                    item.Status = Ready;
                }
                else
                {
                    item.Status = PolicyWrongFunc;
                }
            }
        }

        // --- Workflows ---
        List<AppWorkflowType>? oldWorkflows = Workflows;
        Workflows = schema.Workflows?.Select(w =>
        {
            AppWorkflowType wft = oldWorkflows?.FirstOrDefault(o =>
                o.Name.Equals(w.Name, StringComparison.OrdinalIgnoreCase)) is { Activated: true } old
                ? old : w;
            wft.Application = this;
            return wft;
        }).ToList();

        // --- Preload sub-apps ---
        if (preLoad && Apps is { Length: > 0 })
        {
            foreach (string subName in Apps.Select(a => a.Name))
                await context.GetAppTypeAsync(subName, preload: true);
        }

        // --- Reload referencing apps if foreign-key targets changed ---
        if (!preLoad && reloadApps is { Count: > 0 })
        {
            foreach (AppType app in reloadApps)
                await context.GetAppTypeAsync(app.Name, reload: true);
        }
    }

    /// <summary>Releases all node-type references held by fields and relations.</summary>
    public void Release()
    {
        Fields?.ForEach(f =>
        {
            f.SchemaType?.RemoveUsedBy(f);
            f.FuncNode?.RemoveUsedBy(f);
        });
        Relations?.ForEach(r =>
        {
            if (r.FieldNode != null)
                r.FuncNode?.RemoveUsedBy(r.FieldNode);
        });
        Workflows?.ForEach(w => w.Release());
    }

    /// <summary>Gets all effective auth policies for the given scope, with parent-app inheritance.</summary>
    public IEnumerable<PolicyItem> GetAuthPolicies(PolicyScope scope)
    {
        // System sub-app inherits from root; root delegates to system sub-app first
        if (RootApp == null)
        {
            const string system = "system";
            if (SubAppList?.TryGetValue(system, out AppType? sys) == true)
                foreach (PolicyItem i in sys.GetAuthPolicies(scope))
                    yield return i;
        }
        else if (!Name.Equals("system", StringComparison.OrdinalIgnoreCase))
        {
            foreach (PolicyItem i in RootApp.GetAuthPolicies(scope))
                yield return i;
        }

        // Named policy node items
        // (Auth is a NodeType; if it exposes Items they must be retrieved by the caller)

        // Inline items
        if (Auths != null)
            foreach (PolicyItem i in Auths.Where(p => p.Scope == scope))
                yield return i;
    }

    /// <summary>Gets the field by name (case-insensitive).</summary>
    public AppFieldType? GetField(string? name)
        => !string.IsNullOrWhiteSpace(name)
            ? Fields?.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            : null;

    /// <summary>Gets the workflow by name (case-insensitive).</summary>
    public AppWorkflowType? GetWorkflow(string? name)
        => !string.IsNullOrWhiteSpace(name)
            ? Workflows?.FirstOrDefault(w => w.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            : null;

    #endregion
}
