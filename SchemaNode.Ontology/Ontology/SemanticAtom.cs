namespace SchemaNode.Ontology;

/// <summary>
/// Semantic kind of a <see cref="SemanticAtom"/> — classifies the granularity
/// and structural role of the atom within the ontology.
/// </summary>
public enum SemanticKind
{
    /// <summary>
    /// Full SSP block for a schema entry (backward-compatible; one atom per schema block).
    /// </summary>
    Block,

    /// <summary>
    /// A single value (concept) inside a vocabulary / enum type.
    /// </summary>
    VocabularyValue,

    /// <summary>
    /// A single field inside an entity / struct type.
    /// </summary>
    StructField,

    /// <summary>
    /// A single parameter of a function type.
    /// </summary>
    FunctionParam,

    /// <summary>
    /// A single data-table entry declared on an app container.
    /// </summary>
    AppTable,
}

/// <summary>
/// Smallest embeddable unit of ontology knowledge.
/// Each atom corresponds to one row in the vector store.
/// </summary>
public sealed class SemanticAtom
{
    /// <summary>
    /// Globally unique identifier in full-path form, e.g.
    /// <c>"system_schema_policyscope.schemaCreate"</c> for a vocabulary value
    /// or <c>"system_localetran.lang"</c> for a struct field.
    /// For <see cref="SemanticKind.Block"/> atoms this equals the schema key itself.
    /// </summary>
    public string Id { get; init; } = default!;

    /// <summary>Structural role of this atom.</summary>
    public SemanticKind Kind { get; init; }

    /// <summary>Short name without the parent prefix, e.g. <c>"schemaCreate"</c> or <c>"lang"</c>.</summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// Parent schema key (e.g. the vocabulary or struct name).
    /// <see langword="null"/> for <see cref="SemanticKind.Block"/> atoms.
    /// </summary>
    public string? Parent { get; init; }

    /// <summary>
    /// Natural-language description to embed.  Locale-specific when a locale is known.
    /// </summary>
    public string Content { get; init; } = default!;
}
