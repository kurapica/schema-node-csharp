namespace SchemaNode.AI;

/// <summary>
/// Configuration options for the ontology module.
/// Register via <see cref="SchemaNodeOntologyInjection.AddSchemaOntology"/>.
/// </summary>
public class OntologyOptions
{
    private string _baseUri = SchemaContextOntologyExtension.DefaultBaseUri;

    /// <summary>
    /// Base IRI namespace root used when building ontology graphs.
    /// Defaults to <c>https://schema.local/</c>.
    /// </summary>
    public string BaseUri
    {
        get => _baseUri;
        set => _baseUri = string.IsNullOrWhiteSpace(value)
            ? SchemaContextOntologyExtension.DefaultBaseUri
            : value;
    }

    /// <summary>Gets the currently active options instance.</summary>
    internal static OntologyOptions Current { get; private set; } = new();

    /// <summary>Stores <paramref name="options"/> as the active instance.</summary>
    internal static void Apply(OntologyOptions options) => Current = options;
}
