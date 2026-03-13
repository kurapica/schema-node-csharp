namespace SchemaNode.Vector.Services;

/// <summary>
/// Well-known category values that partition the ontology vector store.
/// Each value corresponds to a distinct ontology dimension, allowing searches
/// to be scoped without embedding category information in the query text.
/// </summary>
public enum OntologyVectorCategory
{
    /// <summary>
    /// Domain-model entries built from an App hierarchy
    /// (<c>BuildAppOntologyAsync</c>).
    /// Covers business entities, domain containers, scope relations and data tables.
    /// Use this category when asking questions about <em>data ownership, business
    /// processes or application structure</em>.
    /// </summary>
    SchemaApp,

    /// <summary>
    /// Schema-type entries built from a type namespace
    /// (<c>BuildSchemaOntologyAsync</c>).
    /// Covers struct types, enum vocabularies, scalar constraints and function contracts.
    /// Use this category when asking questions about <em>data shapes or type
    /// definitions</em>.
    /// </summary>
    SchemaType,
}
