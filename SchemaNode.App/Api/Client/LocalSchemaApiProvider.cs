using System.Text.Json.Nodes;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Property.App;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Schema.Provider;
using NamespaceType = SchemaNode.Runtime.NamespaceType;
using RuntimeAppType = SchemaNode.Runtime.AppType;

namespace SchemaNode.Api.Client;

/// <summary>
/// Local (in-process) Schema API provider.
/// Keeps the TS contract of <see cref="ISchemaApiProvider"/> but reuses the hosting process's
/// <see cref="SchemaContext"/> directly, without going through HTTP.
///
/// Lifetime: constructed by the screen-level <c>SchemaContextFactory</c> when a screen session is created;
/// the constructor argument is that screen's own scoped <see cref="SchemaContext"/>.
/// Therefore <c>SetAccess(app, target)</c> in <see cref="CallFunctionAsync"/>
/// only writes to this screen's context and does not leak across screens.
///
/// Construction still ensures <see cref="SchemaContext.FullMode"/> is true (idempotent):
/// this is a prerequisite for DataNode subscription/cascade/EntrySource to take effect; the factory has
/// already set it beforehand, and this is only a defensive assertion.
/// </summary>
public sealed class LocalSchemaApiProvider : ISchemaApiProvider
{
    private readonly SchemaContext _context;

    /// <summary>
    /// Constructs the provider and ensures full mode is enabled (idempotent: repeated assignment has no side effects).
    /// </summary>
    public LocalSchemaApiProvider(SchemaContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));

        // The screen factory must complete the FullMode=true setup before constructing this provider;
        // perform one more idempotent safeguard here to avoid silently losing reactive capabilities when
        // other hosts misuse it.
        if (!_context.FullMode)
            _context.FullMode = true;
    }

    /// <inheritdoc />
    public async Task<NodeSchema[]> GetSchemaAsync(string[] names, bool includeRefs = false, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(names);

        // The local implementation does not filter by includeRefs: Core already resolves and registers
        // referenced types while loading types; the value returned here is only used for list display,
        // with semantics matching TS's `?.schemas || []`.
        var result = new List<NodeSchema>();
        foreach (string name in names)
        {
            ct.ThrowIfCancellationRequested();
            NodeType? node = await _context.GetNodeTypeAsync(name).ConfigureAwait(false);
            if (node == null) continue;

            // Namespace: return all of its child types together, matching the front-end schema.vue list display
            if (node is NamespaceType ns)
            {
                result.AddRange(ns.GetNodeSchemas(_context));
            }
            else
            {
                NodeSchema? schema = node.GetNodeSchema(_context.Runtime);
                if (schema != null) result.Add(schema);
            }
        }
        return [.. result];
    }

    /// <inheritdoc />
    public async Task<bool> SaveSchemaAsync(NodeSchema schema, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ct.ThrowIfCancellationRequested();

        // Locate the parent namespace and write the schema into it; SaveNodeSchema marks the corresponding
        // type's Loaded as false so it can be reloaded
        NamespaceType? ns = await _context.GetNodeTypeAsync<NamespaceType>(schema.Namespace ?? string.Empty).ConfigureAwait(false);
        if (ns == null) return false;

        ns.SaveNodeSchema(schema);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteSchemaAsync(string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        ct.ThrowIfCancellationRequested();

        NodeType? node = await _context.GetNodeTypeAsync(name).ConfigureAwait(false);
        if (node == null) return false;
        if (node.IsUsed) return false; // Referenced types cannot be deleted, matching the front-end constraint

        string? nsName = node.GetNodeSchema(_context.Runtime)?.Namespace;
        if (string.IsNullOrEmpty(nsName)) return false;

        NamespaceType? ns = await _context.GetNodeTypeAsync<NamespaceType>(nsName).ConfigureAwait(false);
        ns?.RemoveNodeSchema(node.Name);
        return true;
    }

    /// <inheritdoc />
    public Task<JsonNode?> CallFunctionAsync(
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

        // Set up the invocation context (associated app / target data).
        // This context is a screen-level instance, so the settings only affect the current screen;
        // the semantics match the server-side CallFunctionApi.
        if (!string.IsNullOrWhiteSpace(app) || !string.IsNullOrWhiteSpace(target))
            _context.SetAccess(app, target);

        // Extension method on FunctionType: resolves and executes the function internally
        return _context.CallFunctionAsync(schemaName, args, retType, applyMode)!;
    }

    /// <inheritdoc />
    public Task<bool> SaveAppSchemaAsync(AppSchema app, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(app);
        ct.ThrowIfCancellationRequested();
        return _context.SaveAppSchemaAsync(app);
    }

    /// <inheritdoc />
    public async Task<AppSchema?> GetAppSchemaAsync(
        string app,
        bool includeTypes = false,
        string? format = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(app)) return null;
        ct.ThrowIfCancellationRequested();

        // Mirror GetAppSchemaApi: resolve the app type first, then serialize its schema.
        // This also covers built-in (system-defined) apps, which are created by the runtime
        // and are not visible through IAppEntryProvider enumeration.
        RuntimeAppType? node = await _context.GetAppTypeAsync(app).ConfigureAwait(false);
        if (node is null) return null;

        // Same authorization step as the server API (throws when no policy grants SchemaRead).
        await _context.AuthorizeAsync(node, PolicyScope.SchemaRead).ConfigureAwait(false);

        // The format branch of the server API streams a file; this provider can only return
        // the schema itself, so a requested format is ignored here.
        AppSchema schema = await node.GetSchemaAsync(_context).ConfigureAwait(false);

        if (includeTypes)
            schema.NodeSchemas = await _context.GetNodeSchemasAsync(node, includeUsedBy: true, cancellationToken: ct).ConfigureAwait(false);

        if (schema.Fields is not { Length: > 0 })
        {
            List<AppSchema> apps = [];
            foreach (AppSchema sub in node.GetSubAppSchemas())
            {
                RuntimeAppType? subApp = await _context.GetAppTypeAsync(sub.FullName).ConfigureAwait(false);
                if (subApp is not null)
                    apps.Add(await subApp.GetSchemaAsync(_context).ConfigureAwait(false));
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
        return _context.DeleteAppSchemaAsync(app);
    }

    /// <inheritdoc />
    public Task<bool> SaveAppWorkflowSchemaAsync(string app, AppWorkflowSchema workflow, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ct.ThrowIfCancellationRequested();
        return _context.SaveAppWorkflowSchemaAsync(app, workflow);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAppWorkflowSchemaAsync(string app, string workflow, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(workflow)) return Task.FromResult(false);
        ct.ThrowIfCancellationRequested();
        return _context.DeleteAppWorkflowSchemaAsync(app, workflow);
    }
}
