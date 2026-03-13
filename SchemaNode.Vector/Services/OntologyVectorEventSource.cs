using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Ontology;
using SchemaNode.Runtime;

namespace SchemaNode.Vector.Services;

/// <summary>
/// An <see cref="IEventSource"/> that keeps the ontology vector store in sync with
/// schema changes raised via the SchemaNode event system.
/// <para>
/// Subscriptions (started when the application preloads):
/// <list type="bullet">
///   <item><see cref="AppSchemaChangeEvent"/> — re-indexes the changed App's SSP blocks.</item>
///   <item><see cref="AppSchemaDeleteEvent"/> — removes the App's container block from the store.</item>
///   <item><see cref="SchemaChangeEvent"/> — re-indexes the changed schema-type block.</item>
///   <item><see cref="SchemaDeleteEvent"/> — removes the schema-type block from the store.</item>
/// </list>
/// </para>
/// <para>
/// Each event handler runs asynchronously in a background task using a dedicated
/// DI scope so that scoped services (e.g. <c>SchemaContext</c>) are correctly
/// lifetime-managed.
/// </para>
/// </summary>
public sealed class OntologyVectorEventSource : IEventSource
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OntologyVectorEventSource> _logger;
    private readonly List<IDisposable> _subscriptions = [];

    /// <summary>Initialises the event source.</summary>
    public OntologyVectorEventSource(
        IServiceScopeFactory scopeFactory,
        ILogger<OntologyVectorEventSource> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(SchemaContext context, CancellationToken token)
    {
        AddSub(context.SubscribeEvent<AppSchemaChangeEvent>(e =>
            EnqueueAsync(e.Payload?.ToValue<string>(), OntologyVectorCategory.SchemaApp, reindex: true)));

        AddSub(context.SubscribeEvent<AppSchemaDeleteEvent>(e =>
            EnqueueAsync(e.Payload?.ToValue<string>(), OntologyVectorCategory.SchemaApp, reindex: false)));

        AddSub(context.SubscribeEvent<SchemaChangeEvent>(e =>
            EnqueueAsync(e.Payload?.ToValue<string>(), OntologyVectorCategory.SchemaType, reindex: true)));

        AddSub(context.SubscribeEvent<SchemaDeleteEvent>(e =>
            EnqueueAsync(e.Payload?.ToValue<string>(), OntologyVectorCategory.SchemaType, reindex: false)));

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken token)
    {
        foreach (IDisposable sub in _subscriptions) sub.Dispose();
        _subscriptions.Clear();
        return Task.CompletedTask;
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private void AddSub(IDisposable? sub)
    {
        if (sub != null) _subscriptions.Add(sub);
    }

    /// <summary>
    /// Fires-and-forgets an async operation that either re-indexes or deletes the
    /// ontology entry identified by <paramref name="name"/>.
    /// </summary>
    private void EnqueueAsync(string? name, OntologyVectorCategory category, bool reindex)
    {
        if (string.IsNullOrWhiteSpace(name)) return;

        _ = Task.Run(async () =>
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            var vectorService = scope.ServiceProvider.GetService<IOntologyVectorService>();
            if (vectorService == null) return;

            try
            {
                if (!reindex)
                {
                    // Remove the primary ontology block whose schema_key equals the
                    // turtle-safe form of the name (dots/spaces → underscores).
                    string schemaKey = name.Replace('.', '_').Replace(' ', '_');
                    await vectorService.DeleteAsync(schemaKey);
                    _logger.LogInformation(
                        "[OntologyVector] Deleted block '{Key}' ({Category})", schemaKey, category);
                    return;
                }

                var context = scope.ServiceProvider.GetRequiredService<SchemaContext>();

                if (category == OntologyVectorCategory.SchemaApp)
                    await ReIndexAppAsync(name, vectorService, context);
                else
                    await ReIndexTypeAsync(name, vectorService, context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[OntologyVector] Failed to sync {Category} '{Name}': {Message}",
                    category, name, ex.Message);
            }
        });
    }

    /// <summary>
    /// Re-indexes an App only when it is a data-domain app (owns at least one field).
    /// Container apps — those that exist solely to group sub-apps without declaring
    /// fields — are skipped because they carry no embeddable domain semantics,
    /// consistent with <c>InitOntologyVectorsApi</c>.
    /// </summary>
    private async Task ReIndexAppAsync(
        string name, IOntologyVectorService vectorService, SchemaContext context)
    {
        AppType? appType = await context.GetAppTypeAsync(name, preload: true);
        if (appType?.Fields is not { Count: > 0 })
        {
            _logger.LogDebug(
                "[OntologyVector] Skipping app '{Name}': no fields (container)", name);
            return;
        }

        OntologyGraph graph = await context.BuildAppOntologyAsync(name, includeSubApps: false);
        int count = await IndexGraphAsync(graph, OntologyVectorCategory.SchemaApp, vectorService);
        _logger.LogInformation(
            "[OntologyVector] Re-indexed {Count} block(s) for app '{Name}'", count, name);
    }

    /// <summary>
    /// Re-indexes a schema type only when it is a concrete type (struct, enum, scalar,
    /// function).  <see cref="TypeNamespace"/> nodes are structural containers and are
    /// skipped, consistent with <c>InitOntologyVectorsApi</c>.
    /// </summary>
    private async Task ReIndexTypeAsync(
        string name, IOntologyVectorService vectorService, SchemaContext context)
    {
        AnySchemaType? type = await context.GetSchemaTypeAsync(name, preload: true);
        if (type == null || type is TypeNamespace)
        {
            _logger.LogDebug(
                "[OntologyVector] Skipping type '{Name}': namespace or not found", name);
            return;
        }

        OntologyGraph graph = await context.BuildSchemaOntologyAsync(name);
        int count = await IndexGraphAsync(graph, OntologyVectorCategory.SchemaType, vectorService);
        _logger.LogInformation(
            "[OntologyVector] Re-indexed {Count} block(s) for type '{Name}'", count, name);
    }

    /// <summary>
    /// Renders <paramref name="graph"/> as SSP for every locale present in the graph
    /// plus the default <c>"enUS"</c> locale, then upserts each block in the vector store.
    /// Returns the total number of (key, locale) pairs indexed.
    /// </summary>
    private async Task<int> IndexGraphAsync(
        OntologyGraph graph, OntologyVectorCategory category, IOntologyVectorService vectorService)
    {
        int count = 0;

        var extraLocales = OntologyTextTemplates.CollectLocales(graph)
            .Where(l => !l.Equals("enUS", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Block atoms — full SSP text per locale
        string sspDefault = OntologyTextTemplates.Render(graph, OntologyTextTemplates.FormatSsp, null);
        foreach (SemanticAtom atom in OntologySspParser.ParseBlocks(sspDefault))
        {
            await vectorService.IndexAsync(atom, category, "enUS");
            count++;
        }
        foreach (string locale in extraLocales)
        {
            string ssp = OntologyTextTemplates.Render(graph, OntologyTextTemplates.FormatSsp, locale);
            foreach (SemanticAtom atom in OntologySspParser.ParseBlocks(ssp))
            {
                await vectorService.IndexAsync(atom, category, locale);
                count++;
            }
        }

        // Granular atoms — resolved directly from the typed model
        foreach (SemanticAtom atom in OntologySspParser.ParseAtoms(graph, "enUS"))
        {
            await vectorService.IndexAsync(atom, category, "enUS");
            count++;
        }
        foreach (string locale in extraLocales)
        {
            foreach (SemanticAtom atom in OntologySspParser.ParseAtoms(graph, locale))
            {
                await vectorService.IndexAsync(atom, category, locale);
                count++;
            }
        }

        return count;
    }
}
