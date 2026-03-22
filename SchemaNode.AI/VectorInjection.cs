using System.ClientModel;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using OpenAI;
using SchemaNode.AI.Vector;
using SchemaNode.Components;

namespace SchemaNode.AI;

/// <summary>
/// Dependency-injection extensions for the vector feature of <c>SchemaNode.AI</c>.
/// </summary>
public static class SchemaNodeVectorInjection
{
    /// <summary>
    /// Registers the <c>SchemaNode.AI</c> assembly for API discovery, configures
    /// <see cref="OntologyVectorOptions"/>, and sets up the Semantic Kernel with the
    /// appropriate embedding connector based on <see cref="OntologyVectorOptions.Provider"/>.
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
    /// <param name="configureKernel">
    /// Optional delegate for any extra Semantic Kernel builder configuration applied
    /// <em>after</em> the provider connector is registered.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddSchemaVector(
        this IServiceCollection services,
        Action<OntologyVectorOptions>? configure = null,
        Action<IKernelBuilder>? configureKernel = null)
    {
        // Register this assembly so that SchemaApi subclasses defined here are
        // discovered by UseSchemaApis() during request-mapping.
        services.AddSchemaAssemblies(Assembly.GetExecutingAssembly());

        // Configure and register OntologyVectorOptions as a singleton.
        var opts = new OntologyVectorOptions();
        configure?.Invoke(opts);
        services.AddSingleton(opts);

        // Set up the Semantic Kernel and auto-wire the embedding connector from opts.
        IKernelBuilder kernelBuilder = services.AddKernel();
        AddEmbeddingConnector(kernelBuilder, opts);

        // Register the event source that keeps the vector store in sync with schema changes.
        services.AddSingleton<IEventSource, OntologyVectorEventSource>();

        // Allow the caller to add further configuration (additional services, plugins, etc.).
        configureKernel?.Invoke(kernelBuilder);

        return services;
    }

    /// <summary>
    /// Registers the embedding generation connector on <paramref name="builder"/> based on
    /// <see cref="OntologyVectorOptions.Provider"/>.
    /// <list type="bullet">
    ///   <item><see cref="EmbeddingProvider.OpenAI"/> — standard or custom-endpoint OpenAI-compatible server.</item>
    ///   <item><see cref="EmbeddingProvider.AzureOpenAI"/> — Azure OpenAI Service.</item>
    ///   <item><see cref="EmbeddingProvider.Ollama"/> — local Ollama via its OpenAI-compatible <c>/v1</c> API.</item>
    /// </list>
    /// </summary>
    private static void AddEmbeddingConnector(IKernelBuilder builder, OntologyVectorOptions opts)
    {
        switch (opts.Provider)
        {
            case EmbeddingProvider.OpenAI:
                if (opts.Endpoint is { Length: > 0 } customEp)
                {
                    // OpenAI-compatible server at a custom URL (e.g. LM Studio, LocalAI).
                    builder.AddOpenAIEmbeddingGenerator(
                        opts.ModelId,
                        BuildOpenAIClient(opts.ApiKey ?? "", new Uri(customEp)));
                }
                else
                {
                    builder.AddOpenAIEmbeddingGenerator(opts.ModelId, opts.ApiKey ?? "");
                }
                break;

            case EmbeddingProvider.AzureOpenAI:
                builder.AddAzureOpenAIEmbeddingGenerator(
                    deploymentName: opts.DeploymentName ?? opts.ModelId,
                    endpoint:       opts.Endpoint ?? "",
                    apiKey:         opts.ApiKey   ?? "");
                break;

            case EmbeddingProvider.Ollama:
                // Ollama exposes an OpenAI-compatible /v1 endpoint.
                string baseUrl = opts.Endpoint is { Length: > 0 } url
                    ? url.TrimEnd('/')
                    : "http://localhost:11434";
                if (!baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                    baseUrl += "/v1";
                builder.AddOpenAIEmbeddingGenerator(
                    opts.ModelId,
                    BuildOpenAIClient("ollama", new Uri(baseUrl)));  // Ollama ignores the key
                break;
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
}
