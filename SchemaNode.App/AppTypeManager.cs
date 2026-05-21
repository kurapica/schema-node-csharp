using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using SchemaNode.App.Schema;
using SchemaNode.Context;
using SchemaNode.Runtime;

namespace SchemaNode.App;

/// <summary>
/// Extension methods that add App-layer capabilities to Core's <see cref="SchemaContext"/>.
/// These extensions are the bridge between the Core context and the App schema family.
/// </summary>
public static class AppSchemaContextExtensions
{
    /// <summary>
    /// Gets (or creates) an <see cref="AppType"/> by name.
    /// Delegates to the <see cref="IAppTypeManager"/> registered in the DI container.
    /// </summary>
    public static Task<AppType?> GetAppTypeAsync(
        this SchemaContext context,
        string appName,
        bool reload = false,
        bool preload = false)
    {
        IAppTypeManager? manager = context.GetService<IAppTypeManager>();
        return manager == null
            ? Task.FromResult<AppType?>(null)
            : manager.GetAsync(appName, reload, preload);
    }

    /// <summary>
    /// Loads the raw <see cref="AppSchema"/> for the given application name by querying all
    /// registered <see cref="IAppSchemaProvider"/> services and merging their results.
    /// </summary>
    public static async Task<AppSchema?> LoadAppSchemaAsync(
        this SchemaContext context,
        string appName)
    {
        AppSchema? result = null;
        foreach (IAppSchemaProvider provider in context.GetServices<IAppSchemaProvider>())
        {
            AppSchema? schema = await provider.LoadAsync(appName);
            if (schema == null) continue;

            if (result == null)
                result = schema;
            else
                result.CombineCustomSchema(schema);
        }
        return result;
    }
}

/// <summary>
/// Default in-process <see cref="IAppTypeManager"/> backed by a <see cref="ConcurrentDictionary"/>.
/// Wraps a <see cref="SchemaContext"/> and an optional <see cref="IWorkflowEngine"/> to
/// populate <see cref="AppType"/> instances without modifying Core.
/// </summary>
public sealed class AppTypeManager : IAppTypeManager, IAppSchemaContext
{
    private readonly SchemaContext _context;
    private readonly IWorkflowEngine? _engine;
    private readonly ILogger<AppTypeManager> _logger;
    private readonly ConcurrentDictionary<string, AppType> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public AppTypeManager(SchemaContext context, ILogger<AppTypeManager> logger, IWorkflowEngine? engine = null)
    {
        _context = context;
        _logger = logger;
        _engine = engine;
    }

    // ── IAppTypeManager ───────────────────────────────────────────────────────

    public async Task<AppType?> GetAsync(string appName, bool reload = false, bool preload = false)
    {
        if (_cache.TryGetValue(appName, out AppType? cached) && cached.Loaded && !reload)
            return cached;

        await _loadLock.WaitAsync();
        try
        {
            // Double-check after acquiring the lock
            if (_cache.TryGetValue(appName, out cached) && cached.Loaded && !reload)
                return cached;

            AppSchema? schema = await LoadAppSchemaAsync(appName);
            if (schema == null) return null;

            AppType? rootApp = null;
            if (!string.IsNullOrWhiteSpace(schema.Parent))
                rootApp = await GetAsync(schema.Parent);

            AppType app = _cache.GetOrAdd(appName, _ => new AppType
            {
                Name = appName,
                RootApp = rootApp,
            });

            app.Loaded = true;
            await app.LoadAsync(this, schema, preLoad: preload);
            return app;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    public bool Remove(string appName)
    {
        if (_cache.TryRemove(appName, out AppType? removed))
        {
            removed.Release();
            return true;
        }
        return false;
    }

    // ── IAppSchemaContext ─────────────────────────────────────────────────────

    public Task<NodeType?> GetNodeTypeAsync(string schemaName)
        => _context.GetNodeTypeAsync(schemaName);

    public Task<AppType?> GetAppTypeAsync(string appName, bool reload = false, bool preload = false)
        => GetAsync(appName, reload, preload);

    public async Task<AppSchema?> LoadAppSchemaAsync(string appName)
        => await _context.LoadAppSchemaAsync(appName);

    public async Task<JsonNode?> CallFunctionAsync(string functionName, JsonArray args)
    {
        if (await _context.GetNodeTypeAsync(functionName) is not FunctionType func)
            throw new InvalidOperationException($"Function '{functionName}' not found or is not a function type.");
        return await func.CallAsync<JsonNode, SchemaNode.Runtime.Compile.CompileContext>(_context, args.ToArray());
    }

    public void LogWarning(string message, params object[] args)
        => _logger.LogWarning(message, args);

    public void LogError(Exception ex, string message, params object[] args)
        => _logger.LogError(ex, message, args);
}
