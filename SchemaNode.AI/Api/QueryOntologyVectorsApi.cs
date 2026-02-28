using Microsoft.Extensions.DependencyInjection;
using SchemaNode.AI.Services;
using SchemaNode.Components;
using SchemaNode.Http;

namespace SchemaNode.AI.Api;

/// <summary>
/// Performs a semantic similarity search over the indexed ontology vector store.
/// The query text is embedded with the same model used during indexing; the
/// top-K most similar SSP blocks are returned ranked by cosine similarity.
/// </summary>
public class QueryOntologyVectorsApi
    : SchemaApi<QueryOntologyVectorsRequest, QueryOntologyVectorsResponse>
{
    /// <inheritdoc />
    protected override async Task<QueryOntologyVectorsResponse?> ExecuteAsync(
        QueryOntologyVectorsRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return new QueryOntologyVectorsResponse();

        var vectorService = Services.GetRequiredService<IOntologyVectorService>();
        int topK = request.TopK > 0 ? request.TopK : 5;

        IReadOnlyList<OntologyVectorMatch> matches =
            await vectorService.SearchAsync(request.Query, topK, request.Category, SchemaContext.GetLocale(), cancellationToken);

        return new QueryOntologyVectorsResponse
        {
            Matches = matches.Select(m => new OntologyVectorMatchDto
            {
                SchemaKey = m.SchemaKey,
                Category  = m.Category,
                Content   = m.Content,
                Locale    = m.Locale,
                Score     = m.Score,
            }).ToList(),
        };
    }
}

/// <summary>Request for <see cref="QueryOntologyVectorsApi"/>.</summary>
public class QueryOntologyVectorsRequest : SchemaApiRequest
{
    /// <summary>Natural-language or structured query text to embed and search against.</summary>
    public string? Query { get; set; }

    /// <summary>Maximum number of results to return (default 5).</summary>
    public int TopK { get; set; } = 5;

    /// <summary>When set, restricts results to the specified ontology partition.</summary>
    public OntologyVectorCategory? Category { get; set; }
}

/// <summary>Response from <see cref="QueryOntologyVectorsApi"/>.</summary>
public class QueryOntologyVectorsResponse : SchemaApiResponse
{
    /// <summary>Ranked list of matching ontology blocks.</summary>
    public List<OntologyVectorMatchDto> Matches { get; set; } = [];
}

/// <summary>A single match item returned by <see cref="QueryOntologyVectorsApi"/>.</summary>
public class OntologyVectorMatchDto
{
    /// <summary>Schema key of the matching SSP block (e.g. <c>"Order"</c>).</summary>
    public required string SchemaKey { get; init; }

    /// <summary>Ontology partition this block was indexed under.</summary>
    public OntologyVectorCategory Category { get; init; }

    /// <summary>Language tag the block was indexed under (e.g. <c>"enUS"</c>, <c>"zhCN"</c>).</summary>
    public string Locale { get; init; } = "enUS";

    /// <summary>Full SSP block content.</summary>
    public required string Content { get; init; }

    /// <summary>Cosine similarity score in [0, 1].</summary>
    public double Score { get; init; }
}
