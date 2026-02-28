namespace SchemaNode.AI.Services;

/// <summary>
/// Abstracts embedding generation and vector storage for SSP ontology blocks.
/// Implement this interface with a concrete vector-database back-end
/// (e.g. <c>SchemaNode.PostgreSQL.OntologyVectorPostgreSqlService</c>).
/// </summary>
public interface IOntologyVectorService
{
    /// <summary>
    /// Ensures the backing vector table (and its approximate-nearest-neighbour index)
    /// exist with the schema defined by <see cref="OntologyVectorOptions"/>.
    /// Safe to call multiple times — uses <c>CREATE TABLE IF NOT EXISTS</c>.
    /// </summary>
    Task EnsureTableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Embeds the given SSP content block and upserts it in the vector store,
    /// keyed by <paramref name="schemaKey"/> + <paramref name="locale"/>.
    /// Any previously indexed block with the same key and locale is replaced.
    /// </summary>
    /// <param name="schemaKey">
    /// The schema identifier, matching the <c>Schema:</c> header in the SSP block
    /// (e.g. <c>"Order"</c>, <c>"OrderStatus"</c>).
    /// </param>
    /// <param name="sspContent">The full SSP block text to embed.</param>
    /// <param name="category">Ontology partition this block belongs to.</param>
    /// <param name="locale">
    /// Language tag the content was rendered in (e.g. <c>"enUS"</c>, <c>"zhCN"</c>).
    /// Pass <c>"enUS"</c> for the default / key-only rendering.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task IndexAsync(string schemaKey, string sspContent, OntologyVectorCategory category, string locale, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all vectors indexed under <paramref name="schemaKey"/> from the store,
    /// across all locales.
    /// No-op when the key is not present.
    /// </summary>
    /// <param name="schemaKey">The schema key to delete (e.g. <c>"Order"</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(string schemaKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Embeds <paramref name="queryText"/> and returns the top-<paramref name="topK"/>
    /// most similar ontology blocks ranked by cosine similarity.
    /// </summary>
    /// <param name="queryText">Natural-language or structured query text.</param>
    /// <param name="topK">Maximum number of results to return (default 5).</param>
    /// <param name="category">When specified, restricts results to a single ontology partition.</param>
    /// <param name="locale">When specified, restricts results to the given language tag (e.g. <c>"enUS"</c>, <c>"zhCN"</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<OntologyVectorMatch>> SearchAsync(
        string queryText, int topK = 5, OntologyVectorCategory? category = null, string? locale = null, CancellationToken cancellationToken = default);
}
