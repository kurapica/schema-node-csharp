namespace SchemaNode.Ontology.Services;

/// <summary>
/// Selects the embedding back-end wired into the Semantic Kernel.
/// </summary>
public enum EmbeddingProvider
{
    /// <summary>
    /// OpenAI hosted API (<c>api.openai.com</c>).
    /// Set <see cref="OntologyVectorOptions.Endpoint"/> to override with any
    /// OpenAI-compatible server (e.g. a self-hosted LM Studio instance).
    /// </summary>
    OpenAI,

    /// <summary>
    /// Azure OpenAI Service.
    /// Requires <see cref="OntologyVectorOptions.Endpoint"/> (resource URL) and
    /// optionally <see cref="OntologyVectorOptions.DeploymentName"/>.
    /// </summary>
    AzureOpenAI,

    /// <summary>
    /// Local Ollama server (OpenAI-compatible <c>/v1</c> endpoint).
    /// Defaults to <c>http://localhost:11434</c>; override via
    /// <see cref="OntologyVectorOptions.Endpoint"/>.
    /// No API key is required.
    /// </summary>
    Ollama,
}

/// <summary>
/// Configuration for the ontology vector service: embedding model parameters
/// and the PostgreSQL vector-table settings.
/// </summary>
public class OntologyVectorOptions
{
    /// <summary>Default section name in <c>appsettings.json</c>.</summary>
    public const string SectionName = "SchemaNodeAI";

    // ── Embedding provider ─────────────────────────────────────────────────

    /// <summary>
    /// Selects the embedding back-end.
    /// Defaults to <see cref="EmbeddingProvider.OpenAI"/>.
    /// </summary>
    public EmbeddingProvider Provider { get; set; } = EmbeddingProvider.OpenAI;

    /// <summary>
    /// Embedding model identifier.
    /// <list type="bullet">
    ///   <item><b>OpenAI</b>: e.g. <c>"text-embedding-3-small"</c>, <c>"text-embedding-ada-002"</c></item>
    ///   <item><b>Azure OpenAI</b>: same as above; see also <see cref="DeploymentName"/>.</item>
    ///   <item><b>Ollama</b>: e.g. <c>"nomic-embed-text"</c>, <c>"mxbai-embed-large"</c></item>
    /// </list>
    /// </summary>
    public string ModelId { get; set; } = "text-embedding-3-small";

    /// <summary>
    /// API key for authenticated providers.
    /// <list type="bullet">
    ///   <item><b>OpenAI / Azure OpenAI</b>: required.</item>
    ///   <item><b>Ollama</b>: ignored.</item>
    /// </list>
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Optional endpoint URL.
    /// <list type="bullet">
    ///   <item><b>OpenAI</b>: override to point at any OpenAI-compatible server (e.g. LM Studio).</item>
    ///   <item><b>Azure OpenAI</b>: required — resource URL, e.g. <c>https://myresource.openai.azure.com</c>.</item>
    ///   <item><b>Ollama</b>: base URL, defaults to <c>http://localhost:11434</c>.</item>
    /// </list>
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Azure OpenAI deployment name. Falls back to <see cref="ModelId"/> when not set.
    /// Ignored for non-Azure providers.
    /// </summary>
    public string? DeploymentName { get; set; }

    // ── Vector store ───────────────────────────────────────────────────────

    /// <summary>
    /// Number of dimensions for the embedding vectors.
    /// <b>Must contains the model's actual output dimension</b> — this value is used to
    /// define the <c>vector(N)</c> column type when the table is first created.
    /// <list type="bullet">
    ///   <item><c>1536</c> — OpenAI <c>text-embedding-3-small</c> / <c>ada-002</c></item>
    ///   <item><c>3072</c> — OpenAI <c>text-embedding-3-large</c></item>
    ///   <item><c>768</c>  — Ollama <c>nomic-embed-text</c></item>
    ///   <item><c>1024</c> — Ollama <c>mxbai-embed-large</c></item>
    /// </list>
    /// </summary>
    public int Dimensions { get; set; } = 1536;

    /// <summary>PostgreSQL table name used to store ontology embedding vectors.</summary>
    public string TableName { get; set; } = "schema_ontology_vectors";
}
