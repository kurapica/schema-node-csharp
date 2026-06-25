using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;
using System.ClientModel;
using System.Reflection;
using SchemaNode.AI.Vector;
using SchemaNode.Components;
using SchemaNode.Service;
using SchemaNode.Event;

namespace SchemaNode.AI;

/// <summary>
/// Dependency-injection extensions for the vector feature of <c>SchemaNode.AI</c>.
/// </summary>
public static class SchemaNodeVectorInjection
{
    /// <summary>
    /// Registers the <c>SchemaNode.AI</c> assembly for API discovery, configures
    /// <see cref="OntologyVectorOptions"/>, and sets up the embedding generator with the
    /// appropriate connector based on <see cref="OntologyVectorOptions.Provider"/>.
    /// <para>
    /// <b>Must be called before <c>AddSchemaNode</c></b> so that the vector APIs
    /// are included in the assembly scan.
    /// </para>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">
    /// Delegate to configure <see cref="OntologyVectorOptions"/> — typically used to bind
    /// from <c>appsettings.json</c>:
    /// <code>
    /// opts => configuration.GetSection(OntologyVectorOptions.SectionName).Bind(opts)
    /// </code>
    /// </param>
    /// <param name="configureServices">
    /// Optional delegate for any extra service configuration applied
    /// <em>after</em> the embedding generator is registered.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddSchemaVector(
        this IServiceCollection services,
        Action<OntologyVectorOptions>? configure = null,
        Action<IServiceCollection>? configureServices = null)
    {
        // Configure and register OntologyVectorOptions as a singleton.
        var opts = new OntologyVectorOptions();
        configure?.Invoke(opts);
        services.AddSingleton(opts);

        // Register the embedding generator directly from the configured provider.
        services.AddSingleton(CreateEmbeddingGenerator(opts));

        // Register the event source that keeps the vector store in sync with schema changes.
        services.AddSingleton<IEventSource, OntologyVectorEventSource>();

        // Allow the caller to add further configuration (additional services, etc.).
        configureServices?.Invoke(services);

        return services.AddAppSchemaAssemblies(Assembly.GetExecutingAssembly());
    }

    /// <summary>
    /// Creates an <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/> based on
    /// <see cref="OntologyVectorOptions.Provider"/>.
    /// <list type="bullet">
    ///   <item><see cref="EmbeddingProvider.OpenAI"/> — standard or custom-endpoint OpenAI-compatible server.</item>
    ///   <item><see cref="EmbeddingProvider.AzureOpenAI"/> — Azure OpenAI Service.</item>
    ///   <item><see cref="EmbeddingProvider.Ollama"/> — local Ollama via its OpenAI-compatible <c>/v1</c> API.</item>
    /// </list>
    /// </summary>
    private static IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(OntologyVectorOptions opts)
    {
        switch (opts.Provider)
        {
            case EmbeddingProvider.OpenAI:
                if (opts.Endpoint is { Length: > 0 } customEp)
                {
                    // OpenAI-compatible server at a custom URL (e.g. LM Studio, LocalAI).
                    return new OpenAIEmbeddingGeneratorAdapter(
                        BuildOpenAIClient(opts.ApiKey ?? "", new Uri(customEp))
                            .GetEmbeddingClient(opts.ModelId),
                        opts.ModelId);
                }
                else
                {
                    return new OpenAIEmbeddingGeneratorAdapter(
                        new OpenAIClient(new ApiKeyCredential(opts.ApiKey ?? ""))
                            .GetEmbeddingClient(opts.ModelId),
                        opts.ModelId);
                }

            case EmbeddingProvider.AzureOpenAI:
                return new OpenAIEmbeddingGeneratorAdapter(
                    new AzureOpenAIClient(
                            new Uri(opts.Endpoint ?? ""),
                            new ApiKeyCredential(opts.ApiKey ?? ""))
                        .GetEmbeddingClient(opts.DeploymentName ?? opts.ModelId),
                    opts.DeploymentName ?? opts.ModelId);

            case EmbeddingProvider.Ollama:
                // Ollama exposes an OpenAI-compatible /v1 endpoint.
                string baseUrl = opts.Endpoint is { Length: > 0 } url
                    ? url.TrimEnd('/')
                    : "http://localhost:11434";
                if (!baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                    baseUrl += "/v1";
                return new OpenAIEmbeddingGeneratorAdapter(
                    BuildOpenAIClient("ollama", new Uri(baseUrl))  // Ollama ignores the key
                        .GetEmbeddingClient(opts.ModelId),
                    opts.ModelId);

            default:
                throw new InvalidOperationException($"Unsupported embedding provider: {opts.Provider}");
        }
    }

    /// <summary>
    /// Creates an <see cref="OpenAIClient"/> pointing at <paramref name="endpoint"/>.
    /// Used for Ollama and custom OpenAI-compatible servers where the SDK default
    /// base URL must be overridden.
    /// </summary>
    private static OpenAIClient BuildOpenAIClient(string apiKey, Uri endpoint)
    {
        var options = new OpenAIClientOptions { Endpoint = endpoint };
        return new OpenAIClient(new ApiKeyCredential(apiKey), options);
    }

    // ── Adapter ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Adapts <see cref="EmbeddingClient"/> to <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/>
    /// without a dependency on Microsoft.SemanticKernel or Microsoft.Extensions.AI.OpenAI
    /// extension methods.
    /// </summary>
    private sealed class OpenAIEmbeddingGeneratorAdapter(OpenAI.Embeddings.EmbeddingClient client, string modelId)
        : IEmbeddingGenerator<string, Embedding<float>>
    {
        /// <inheritdoc />
        public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var result = new GeneratedEmbeddings<Embedding<float>>();
            foreach (string value in values)
            {
                var response = await client.GenerateEmbeddingAsync(value, cancellationToken: cancellationToken);
                result.Add(new Embedding<float>(response.Value.ToFloats()) { ModelId = modelId });
            }
            return result;
        }

        /// <inheritdoc />
        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceType.IsInstanceOfType(this) ? this : null;

        /// <inheritdoc />
        public void Dispose() { }
    }
}
