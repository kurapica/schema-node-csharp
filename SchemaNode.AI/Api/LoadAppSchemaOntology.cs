using System.Text;
using Microsoft.Extensions.Logging;
using SchemaNode.Context;
using SchemaNode.Http;
using SchemaNode.Property.App;
using SchemaNode.Runtime;

namespace SchemaNode.AI.Api;

/// <summary>
/// Returns the ontology (semantic layer) of an App schema or schema-type namespace in one of
/// the built-in text formats: <c>turtle</c> (OWL/Turtle RDF, default), <c>markdown</c>,
/// <c>jsonld</c>, or <c>ssp</c> (Semantic Schema Projection).
/// <para>
/// Set <see cref="LoadAppSchemaOntologyRequest.App"/> to query an App hierarchy, or
/// <see cref="LoadAppSchemaOntologyRequest.Namespace"/> to query a schema-type namespace
/// (or a single named schema type).
/// </para>
/// </summary>
public class LoadAppSchemaOntologyApi : SchemaApi<LoadAppSchemaOntologyRequest, LoadAppSchemaOntologyResponse>
{
    /// <inheritdoc />
    protected override async Task<LoadAppSchemaOntologyResponse?> ExecuteAsync(
        LoadAppSchemaOntologyRequest request, CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]LoadAppSchemaOntology [Request]{request}", request);

        string baseUri = string.IsNullOrWhiteSpace(request.BaseUri)
            ? "https://schema.local/"
            : request.BaseUri;

        // Fix 1: reject relative IRIs — fall back to the default absolute base
        if (!Uri.TryCreate(baseUri, UriKind.Absolute, out _))
            baseUri = "https://schema.local/";

        string format = string.IsNullOrWhiteSpace(request.Format)
            ? OntologyTextTemplates.FormatTurtle
            : request.Format.ToLowerInvariant();

        OntologyGraph graph;
        string safeName;

        if (!string.IsNullOrWhiteSpace(request.App))
        {
            // ── App path (existing logic, unchanged) ─────────────────────
            AppType? node = await SchemaContext.GetAppTypeAsync(request.App);
            if (node == null) return new LoadAppSchemaOntologyResponse();

            await SchemaContext.AuthorizeAsync(node, PolicyScope.SchemaRead);

            graph = await SchemaContext.BuildAppOntologyAsync(
                request.App,
                baseUri,
                request.IncludeSubApps,
                cancellationToken);

            safeName = request.App.Replace('.', '_').Replace(' ', '_');
        }
        else if (!string.IsNullOrWhiteSpace(request.Namespace))
        {
            // ── Schema-type / namespace path ──────────────────────────────
            graph = await SchemaContext.BuildSchemaOntologyAsync(
                request.Namespace,
                baseUri,
                cancellationToken);

            safeName = request.Namespace.Replace('.', '_').Replace(' ', '_');
        }
        else
        {
            return new LoadAppSchemaOntologyResponse();
        }

        string content = OntologyTextTemplates.Render(graph, format, SchemaContext.GetLocale());

        // JSON-LD is already JSON — return it inline so callers can parse it directly.
        // All other formats are plain-text and are returned as a file download.
        if (format == OntologyTextTemplates.FormatJsonLd)
        {
            return new LoadAppSchemaOntologyResponse
            {
                Format  = format,
                Content = content,
                Graph   = graph,
            };
        }

        string ext = format switch
        {
            OntologyTextTemplates.FormatMarkdown => "md",
            OntologyTextTemplates.FormatSsp      => "ssp",
            _                                    => "ttl",
        };

        return new LoadAppSchemaOntologyResponse
        {
            Format = format,
            Output = new SchemaApiFile
            {
                Name   = $"{safeName}_ontology.{ext}",
                Stream = new MemoryStream(Encoding.UTF8.GetBytes(content)),
            },
        };
    }
}

/// <summary>
/// Request for <see cref="LoadAppSchemaOntologyApi"/>.
/// <para>
/// Exactly one of <see cref="App"/> or <see cref="Namespace"/> must be provided:
/// <list type="bullet">
///   <item><see cref="App"/> — builds from an App schema hierarchy (existing behaviour).</item>
///   <item><see cref="Namespace"/> — builds from a schema-type namespace or single named type.</item>
/// </list>
/// </para>
/// </summary>
public class LoadAppSchemaOntologyRequest : SchemaApiRequest
{
    /// <summary>
    /// App schema name (e.g. <c>"order"</c> or <c>"order.item"</c>).
    /// Takes priority over <see cref="Namespace"/> when both are provided.
    /// </summary>
    public string? App { get; set; }

    /// <summary>
    /// Schema-type namespace or single type name
    /// (e.g. <c>"gevent.event.session"</c> or <c>"gevent.event.session.session"</c>).
    /// When the value resolves to a namespace all descendant types are included.
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// Output format: <c>turtle</c> (default), <c>markdown</c>, <c>jsonld</c>, or <c>ssp</c>
    /// (Semantic Schema Projection — machine-optimised text for vector embedding).
    /// </summary>
    public string Format { get; set; } = OntologyTextTemplates.FormatTurtle;

    /// <summary>
    /// Base IRI used as namespace root in all generated IRIs.
    /// Defaults to <c>https://schema.local/</c>.
    /// </summary>
    public string? BaseUri { get; set; }

    /// <summary>
    /// Whether to include sub-applications as sub-classes.
    /// Only relevant when <see cref="App"/> is set. Defaults to <see langword="true"/>.
    /// </summary>
    public bool IncludeSubApps { get; set; } = true;
}

/// <summary>
/// Response from <see cref="LoadAppSchemaOntologyApi"/>.
/// <para>
/// For <c>jsonld</c> format the response is returned as a normal JSON body
/// with <see cref="Content"/> and <see cref="Graph"/> populated.<br/>
/// For <c>turtle</c> (<c>.ttl</c>) and <c>markdown</c> (<c>.md</c>) formats
/// the rendered text is delivered as a file download via <see cref="SchemaApiResponse.Output"/>.
/// </para>
/// </summary>
public class LoadAppSchemaOntologyResponse : SchemaApiResponse
{
    /// <summary>
    /// The format identifier used for this response
    /// (<c>turtle</c>, <c>markdown</c>, or <c>jsonld</c>).
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// The rendered JSON-LD text. Populated only when <see cref="Format"/> is <c>jsonld</c>.
    /// For other formats the content is streamed as a file via <see cref="SchemaApiResponse.Output"/>.
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// The structured ontology graph for client-side processing or custom rendering.
    /// Populated only when <see cref="Format"/> is <c>jsonld</c>.
    /// </summary>
    public OntologyGraph? Graph { get; set; }
}
