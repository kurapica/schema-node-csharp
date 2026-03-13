using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchemaNode.Vector.Services;
using SchemaNode.Enum;
using SchemaNode.Http;
using SchemaNode.Ontology;
using SchemaNode.Runtime;

namespace SchemaNode.Vector.Api;

/// <summary>
/// Bulk-initialises the ontology vector store by indexing every App and every
/// user-defined schema-type namespace currently registered in the SchemaContext.
/// <para>
/// Suitable for a one-time setup call after the server starts, or to rebuild the
/// vector store from scratch. Existing vectors for each block are replaced via the
/// normal upsert logic (<c>IndexAsync</c>).
/// </para>
/// </summary>
public class InitOntologyVectorsApi
    : SchemaApi<InitOntologyVectorsRequest, InitOntologyVectorsResponse>
{
    /// <inheritdoc />
    protected override async Task<InitOntologyVectorsResponse?> ExecuteAsync(
        InitOntologyVectorsRequest request, CancellationToken cancellationToken)
    {
        var vectorService = Services.GetRequiredService<IOntologyVectorService>();
        await vectorService.EnsureTableAsync(cancellationToken);

        string baseUri = string.IsNullOrWhiteSpace(request.BaseUri)
            ? "https://schema.local/"
            : request.BaseUri;

        int appBlockCount  = 0;
        int typeBlockCount = 0;

        // ── Apps ─────────────────────────────────────────────────────────────
        // Walk the full app tree and collect only data-domain apps (those that
        // own fields).  Container apps — apps that exist only to group sub-apps
        // without declaring any fields — are intentionally skipped because they
        // carry no domain semantics worth embedding.
        // Each data app is indexed independently (includeSubApps:false) so that
        // every app produces its own focused set of SSP blocks.
        AppType? rootApp = await SchemaContext.GetAppTypeAsync("", preload: true);
        var dataApps = new List<AppType>();
        CollectDataApps(rootApp, dataApps);

        foreach (AppType app in dataApps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                OntologyGraph graph = await SchemaContext.BuildAppOntologyAsync(
                    app.Name, baseUri, includeSubApps: false, cancellationToken);
                appBlockCount += await IndexGraphWithLocalesAsync(
                    graph, OntologyVectorCategory.SchemaApp, vectorService, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.LogWarning(ex,
                    "[InitOntologyVectors] Skipping app '{App}': {Message}", app.Name, ex.Message);
            }
        }

        // ── Schema types ──────────────────────────────────────────────────────
        // Walk the full namespace tree and collect only concrete (non-namespace),
        // non-system schema types.  TypeNamespace nodes are purely structural
        // containers and carry no embeddable semantics of their own.
        TypeNamespace? rootNs = await SchemaContext.GetSchemaTypeAsync("", preload: true) as TypeNamespace;
        var concreteTypes = new List<AnySchemaType>();
        if (rootNs != null) CollectConcreteTypes(rootNs, concreteTypes);

        foreach (AnySchemaType type in concreteTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                OntologyGraph graph = await SchemaContext.BuildSchemaOntologyAsync(
                    type.Name, baseUri, cancellationToken);
                typeBlockCount += await IndexGraphWithLocalesAsync(
                    graph, OntologyVectorCategory.SchemaType, vectorService, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.LogWarning(ex,
                    "[InitOntologyVectors] Skipping type '{Type}': {Message}", type.Name, ex.Message);
            }
        }

        return new InitOntologyVectorsResponse
        {
            AppBlockCount  = appBlockCount,
            TypeBlockCount = typeBlockCount,
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Renders <paramref name="graph"/> as SSP for every locale present in the graph
    /// plus the default <c>"enUS"</c> locale, then upserts each block in the vector store.
    /// Returns the total number of (key, locale) pairs indexed.
    /// </summary>
    private static async Task<int> IndexGraphWithLocalesAsync(
        OntologyGraph graph,
        OntologyVectorCategory category,
        IOntologyVectorService vectorService,
        CancellationToken cancellationToken)
    {
        int count = 0;

        var extraLocales = OntologyTextTemplates.CollectLocales(graph)
            .Where(l => !l.Equals("enUS", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Block atoms — full SSP text, one block per schema entry per locale
        string sspDefault = OntologyTextTemplates.Render(graph, OntologyTextTemplates.FormatSsp, null);
        foreach (SemanticAtom atom in OntologySspParser.ParseBlocks(sspDefault))
        {
            await vectorService.IndexAsync(atom, category, "enUS", cancellationToken);
            count++;
        }
        foreach (string locale in extraLocales)
        {
            string ssp = OntologyTextTemplates.Render(graph, OntologyTextTemplates.FormatSsp, locale);
            foreach (SemanticAtom atom in OntologySspParser.ParseBlocks(ssp))
            {
                await vectorService.IndexAsync(atom, category, locale, cancellationToken);
                count++;
            }
        }

        // Granular atoms — resolved directly from the typed model (labels already include DB locale data)
        foreach (SemanticAtom atom in OntologySspParser.ParseAtoms(graph, "enUS"))
        {
            await vectorService.IndexAsync(atom, category, "enUS", cancellationToken);
            count++;
        }
        foreach (string locale in extraLocales)
        {
            foreach (SemanticAtom atom in OntologySspParser.ParseAtoms(graph, locale))
            {
                await vectorService.IndexAsync(atom, category, locale, cancellationToken);
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Recursively collects every <see cref="AppType"/> that owns at least one
    /// field (i.e. is a data-domain app, not a structural container).
    /// </summary>
    private static void CollectDataApps(AppType? root, List<AppType> result)
    {
        if (root == null) return;
        if (root.Fields is { Count: > 0 })
            result.Add(root);
        if (root.SubAppList == null) return;
        foreach (AppType sub in root.SubAppList.Values)
            CollectDataApps(sub, result);
    }

    /// <summary>
    /// Recursively collects every non-namespace, non-system <see cref="AnySchemaType"/>
    /// reachable from <paramref name="ns"/>.
    /// <see cref="TypeNamespace"/> nodes are containers — they are traversed but not
    /// added to the result set.
    /// </summary>
    private static void CollectConcreteTypes(TypeNamespace ns, List<AnySchemaType> result)
    {
        foreach (AnySchemaType type in ns.SchemaNodes.Values)
        {
            if ((type.LoadState & SchemaLoadState.System) != 0)
                continue;
            if (type is TypeNamespace subNs)
                CollectConcreteTypes(subNs, result);
            else
                result.Add(type);
        }
    }
}

/// <summary>Request for <see cref="InitOntologyVectorsApi"/>.</summary>
public class InitOntologyVectorsRequest : SchemaApiRequest
{
    /// <summary>Base IRI namespace root. Defaults to <c>https://schema.local/</c>.</summary>
    public string? BaseUri { get; set; }
}

/// <summary>Response from <see cref="InitOntologyVectorsApi"/>.</summary>
public class InitOntologyVectorsResponse : SchemaApiResponse
{
    /// <summary>Total number of App SSP blocks indexed.</summary>
    public int AppBlockCount { get; set; }

    /// <summary>Total number of schema-type SSP blocks indexed.</summary>
    public int TypeBlockCount { get; set; }

    /// <summary>Combined total of all indexed blocks.</summary>
    public int TotalBlockCount => AppBlockCount + TypeBlockCount;
}
