using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Property.App;
using SchemaNode.Property.Function;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Schema.Provider;
using NamespaceType = SchemaNode.Runtime.NamespaceType;
using RuntimeAppType = SchemaNode.Runtime.AppType;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Api.Client;

/// <summary>
/// Local (in-process) Schema API provider.
/// Keeps the TS contract of <see cref="ISchemaApiProvider"/> but reuses the hosting process's
/// <see cref="SchemaContext"/> directly, without going through HTTP.
///
/// Every method mirrors the server-side API implementation in <c>SchemaNode.Api.Schema.Edit</c>
/// and <c>SchemaNode.Api.Schema.Info</c>: it performs the same <see cref="SchemaContext"/> call,
/// including authorization, storage-provider writes and runtime reloads, instead of inventing
/// an alternative storage layout. The low-level provider is never bypassed or reimplemented here.
///
/// Lifetime: constructed by the screen-level <c>SchemaContextFactory</c> when a screen session is created.
/// The constructor argument is the screen's own FullMode <see cref="SchemaContext"/>, but it is used
/// only as the anchor for services and as the locale source. Every method runs on a dedicated,
/// lazily created, non-FullMode <see cref="SchemaContext"/> owned by this provider:
/// a FullMode context propagates relations to DataNodes while saving, which can leave the Core
/// load lock held and make the subsequent forced reload a no-op. Schema API work needs no DataNode
/// propagation, so it runs on the non-FullMode context.
///
/// The temporary context lives in its own DI scope created from the process root, so it shares the
/// singleton <c>ISchemaRuntime</c> and the same backing store, while getting its own scoped services
/// (storage provider, context items) bound to it. It is not registered in the screen factory's live
/// context list and owns no UI node; only schema data crosses the API boundary. The scope is disposed
/// with this provider, which the screen session disposes.
/// </summary>
public sealed class LocalSchemaApiProvider : ISchemaApiProvider, IDisposable
{
    private readonly SchemaContext _host;
    private readonly IServiceScopeFactory _scopeFactory;

    private IServiceScope? _scope;
    private SchemaContext? _context;
    private bool _disposed;
    private readonly object _gate = new();

    /// <summary>
    /// Stores the hosting screen context and the scope factory used to create the temporary context.
    /// </summary>
    public LocalSchemaApiProvider(SchemaContext host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _scopeFactory = host.GetRequiredService<IServiceScopeFactory>();
    }

    /// <summary>
    /// The temporary non-FullMode context. Created on first use; its locale follows the host
    /// context so that messages produced after a language switch use the new language.
    /// </summary>
    private SchemaContext Context
    {
        get
        {
            SchemaContext context;
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(LocalSchemaApiProvider));

                if (_context is null)
                {
                    _scope = _scopeFactory.CreateScope();
                    _context = _scope.ServiceProvider.GetRequiredService<SchemaContext>();
                }

                context = _context;
            }

            // The temporary context keeps its own Locale; syncing per call makes it follow the host
            // context, so errors raised after a language switch use the new language.
            context.Locale = _host.Locale;
            return context;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        IServiceScope? scope;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;

            // Disposing the scope disposes the scoped temporary SchemaContext and every service bound to it.
            _context = null;
            scope = _scope;
            _scope = null;
        }

        scope?.Dispose();
    }

    /// <inheritdoc />
    public async Task<NodeSchema[]> GetSchemaAsync(string[] names, bool includeRefs = false, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(names);

        // Same implementation as GetSchemaApi: build a root namespace schema, resolve each requested
        // node with a SchemaRead check, and let GetNodeSchemasAsync install the full namespace path.
        // A namespace is therefore expanded into its definition plus its direct children, matching
        // exactly what a remote client receives; the result is a tree, not a flat child list.
        NodeSchema root = new()
        {
            Name = "",
            Kind = NODE_KIND_NAMESPACE,
            Schemas = [],
        };
        HashSet<string> types = [];

        foreach (string name in names)
        {
            ct.ThrowIfCancellationRequested();
            NodeType? node = await Context.GetNodeTypeAsync(name, token: ct).ConfigureAwait(false);
            await GetNodeSchemas(node, true).ConfigureAwait(false);
        }

        return root.Schemas ?? [];

        async Task GetNodeSchemas(NodeType? node, bool first = false)
        {
            if (node == null) return;
            if (await Context.AuthorizeAsync(node, PolicyScope.SchemaRead, true, ct).ConfigureAwait(false) == false) return;

            await Context.GetNodeSchemasAsync(node, root, types, includeRefs, cancellationToken: ct).ConfigureAwait(false);

            if (node is NamespaceType ns && first)
                foreach (var pair in ns.GetNodeSchemas())
                {
                    ct.ThrowIfCancellationRequested();
                    NodeType? nodeType = await Context.GetNodeTypeAsync(pair.FullName, token: ct).ConfigureAwait(false);
                    if (nodeType != null)
                        await GetNodeSchemas(nodeType).ConfigureAwait(false);
                }
        }
    }

    /// <inheritdoc />
    public async Task<bool> SaveSchemaAsync(NodeSchema schema, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ct.ThrowIfCancellationRequested();

        // Same implementation as SaveSchemaApi: authorize, persist through the storage provider,
        // refresh the runtime node from the reload path, raise the schema-change event.
        return await Context.SaveSchemaAsync(schema, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteSchemaAsync(string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        ct.ThrowIfCancellationRequested();

        // Same implementation as DeleteSchemaApi: the Core extension owns the IsUsed check,
        // SchemaDelete authorization, provider deletion and runtime removal.
        return await Context.DeleteSchemaAsync(name, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<JsonNode?> CallFunctionAsync(
        string schemaName,
        JsonArray? args = null,
        string? retType = null,
        ApplyMode applyMode = ApplyMode.Call,
        string? app = null,
        string? target = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        args ??= new JsonArray();

        // Same implementation as CallFunctionApi: reject non-function nodes and workflow-only
        // functions, then set the access context and authorize FuncExecute before invoking.
        NodeType? node = await Context.GetNodeTypeAsync(schemaName, token: ct).ConfigureAwait(false);
        if (node is not FunctionType func || func.HasFlag<WorkflowOnly>() == true) return null;

        if (!string.IsNullOrWhiteSpace(app) || !string.IsNullOrWhiteSpace(target))
            Context.SetAccess(app, target);

        await Context.AuthorizeAsync(node, PolicyScope.FuncExecute, token: ct).ConfigureAwait(false);

        return await Context.CallFunctionAsync(func, args, retType, applyMode, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<bool> SaveAppSchemaAsync(AppSchema app, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(app);
        ct.ThrowIfCancellationRequested();
        return Context.SaveAppSchemaAsync(app, ct);
    }

    /// <inheritdoc />
    public async Task<AppSchema?> GetAppSchemaAsync(
        string app,
        bool includeTypes = false,
        string? format = null,
        CancellationToken ct = default)
    {
        // The empty name is the root app: the server API accepts it and enumerates root-level apps.
        // Keeping it here lets the administrator app list browse from the root like appSchema.vue.
        ct.ThrowIfCancellationRequested();

        // Mirror GetAppSchemaApi: resolve the app type first, then serialize its schema.
        // This also covers built-in (system-defined) apps, which are created by the runtime
        // and are not visible through IAppEntryProvider enumeration.
        RuntimeAppType? node = await Context.GetAppTypeAsync(app, token: ct).ConfigureAwait(false);
        if (node is null) return null;

        // Same authorization step as the server API (throws when no policy grants SchemaRead).
        await Context.AuthorizeAsync(node, PolicyScope.SchemaRead, token: ct).ConfigureAwait(false);

        // The format branch of the server API streams a file; this provider can only return
        // the schema itself, so a requested format is ignored here.
        AppSchema schema = await node.GetSchemaAsync(Context, token: ct).ConfigureAwait(false);

        if (includeTypes)
            schema.NodeSchemas = await Context.GetNodeSchemasAsync(node, includeUsedBy: true, cancellationToken: ct).ConfigureAwait(false);

        if (schema.Fields is not { Length: > 0 })
        {
            List<AppSchema> apps = [];
            foreach (AppSchema sub in node.GetSubAppSchemas())
            {
                RuntimeAppType? subApp = await Context.GetAppTypeAsync(sub.FullName, token: ct).ConfigureAwait(false);
                if (subApp is not null)
                    apps.Add(await subApp.GetSchemaAsync(Context, token: ct).ConfigureAwait(false));
            }
            schema.Apps = apps.ToArray();
        }

        return schema;
    }

    /// <inheritdoc />
    public Task<bool> DeleteAppSchemaAsync(string app, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(app)) return Task.FromResult(false);
        ct.ThrowIfCancellationRequested();
        return Context.DeleteAppSchemaAsync(app, ct);
    }

    /// <inheritdoc />
    public Task<bool> SaveAppWorkflowSchemaAsync(string app, AppWorkflowSchema workflow, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ct.ThrowIfCancellationRequested();
        return Context.SaveAppWorkflowSchemaAsync(app, workflow, token: ct);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAppWorkflowSchemaAsync(string app, string workflow, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(workflow)) return Task.FromResult(false);
        ct.ThrowIfCancellationRequested();
        return Context.DeleteAppWorkflowSchemaAsync(app, workflow, ct);
    }
}
