namespace SchemaNode.Ontology.Services;

/// <summary>A single result returned from a vector similarity search over the ontology store.</summary>
public class OntologyVectorMatch
{
    /// <summary>
    /// The atom identifier (e.g. <c>"Order"</c> for a block, <c>"Order.status"</c> for a field atom).
    /// Corresponds to <see cref="SemanticAtom.Id"/>.
    /// </summary>
    public required string SchemaKey { get; init; }

    /// <summary>Structural role of the matched atom.</summary>
    public SemanticKind Kind { get; init; } = SemanticKind.Block;

    /// <summary>
    /// Short name without the parent prefix (e.g. <c>"status"</c>).
    /// Equals <see cref="SchemaKey"/> for <see cref="SemanticKind.Block"/> atoms.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Parent schema key for granular atoms (e.g. <c>"Order"</c> for <c>"Order.status"</c>).
    /// <see langword="null"/> for <see cref="SemanticKind.Block"/> atoms.
    /// </summary>
    public string? Parent { get; init; }

    /// <summary>Category this atom was indexed under (see <see cref="OntologyVectorCategory"/>).</summary>
    public required OntologyVectorCategory Category { get; init; }

    /// <summary>The content text that was embedded and stored.</summary>
    public required string Content { get; init; }

    /// <summary>
    /// BCP-47 / normalised language tag the atom was indexed under (e.g. <c>"enUS"</c>, <c>"zhCN"</c>).
    /// Defaults to <c>"enUS"</c> for atoms indexed without an explicit locale.
    /// </summary>
    public string Locale { get; init; } = "enUS";

    /// <summary>Cosine similarity score in [0, 1]; higher means more similar to the query.</summary>
    public double Score { get; init; }
}
