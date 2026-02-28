namespace SchemaNode.AI.Services;

/// <summary>A single result returned from a vector similarity search over the ontology store.</summary>
public class OntologyVectorMatch
{
    /// <summary>
    /// The SSP block schema key (e.g. <c>"Order"</c>, <c>"OrderStatus"</c>).
    /// This is the value of the <c>Schema:</c> header line in the SSP block.
    /// </summary>
    public required string SchemaKey { get; init; }

    /// <summary>Category this block was indexed under (see <see cref="OntologyVectorCategory"/>).</summary>
    public required OntologyVectorCategory Category { get; init; }

    /// <summary>The full SSP block content that was embedded and stored.</summary>
    public required string Content { get; init; }

    /// <summary>
    /// BCP-47 / normalised language tag the block was indexed under (e.g. <c>"enUS"</c>, <c>"zhCN"</c>).
    /// Defaults to <c>"enUS"</c> for blocks indexed without an explicit locale.
    /// </summary>
    public string Locale { get; init; } = "enUS";

    /// <summary>Cosine similarity score in [0, 1]; higher means more similar to the query.</summary>
    public double Score { get; init; }
}
