namespace SchemaNode.AI.Agents;

/// <summary>
/// Configuration for the agent provider used by the AI workflow node.
/// The defaults target a local Ollama instance and can be overridden through
/// dependency injection or environment variables.
/// </summary>
public sealed class AIAgentOptions
{
    /// <summary>Default configuration section name in <c>appsettings.json</c>.</summary>
    public const string SectionName = "SchemaAIWorkflow";

    /// <summary>Environment variable selecting the provider (<c>mock</c> or <c>ollama</c>).</summary>
    public const string ProviderEnvironment = "SCHEMANODE_AI_PROVIDER";

    /// <summary>Environment variable overriding the Ollama base URL.</summary>
    public const string EndpointEnvironment = "SCHEMANODE_AI_OLLAMA_ENDPOINT";

    /// <summary>Environment variable overriding the Ollama model name.</summary>
    public const string ModelEnvironment = "SCHEMANODE_AI_OLLAMA_MODEL";

    /// <summary>Environment variable overriding the per-request timeout in seconds.</summary>
    public const string TimeoutEnvironment = "SCHEMANODE_AI_TIMEOUT_SECONDS";

    /// <summary>Environment variable overriding the validation retry count.</summary>
    public const string MaxRetriesEnvironment = "SCHEMANODE_AI_MAX_RETRIES";

    /// <summary>The default provider.</summary>
    public AIProviderKind Provider { get; set; } = AIProviderKind.Ollama;

    /// <summary>The Ollama base URL.</summary>
    public string Endpoint { get; set; } = "http://localhost:11434";

    /// <summary>The Ollama model name.</summary>
    public string Model { get; set; } = "qwen3:8b";

    /// <summary>Per-request timeout in seconds. A request that exceeds it fails with a typed error.</summary>
    public int RequestTimeoutSeconds { get; set; } = 120;

    /// <summary>Additional generation attempts after the first validation failure.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>Maximum completion tokens. Kept small by default for local GPUs.</summary>
    public int MaxTokens { get; set; } = 512;

    /// <summary>The sampling temperature.</summary>
    public double Temperature { get; set; } = 0.2;

    /// <summary>Maximum target schema description characters injected into the prompt.</summary>
    public int MaxMetadataChars { get; set; } = 6000;

    /// <summary>
    /// Deterministic responses used by <see cref="MockAgentProvider"/>. Empty means <c>{}</c>.
    /// </summary>
    public string[] MockResponses { get; set; } = [];

    /// <summary>
    /// Creates the options from environment variables, falling back to the built-in defaults.
    /// </summary>
    public static AIAgentOptions FromEnvironment()
    {
        AIAgentOptions options = new();

        string? provider = Environment.GetEnvironmentVariable(ProviderEnvironment);
        if (!string.IsNullOrWhiteSpace(provider))
        {
            switch (provider.Trim().ToLowerInvariant())
            {
                case "mock":
                    options.Provider = AIProviderKind.Mock;
                    break;
                case "ollama":
                    options.Provider = AIProviderKind.Ollama;
                    break;
            }
        }

        string? endpoint = Environment.GetEnvironmentVariable(EndpointEnvironment);
        if (!string.IsNullOrWhiteSpace(endpoint))
            options.Endpoint = endpoint.Trim();

        string? model = Environment.GetEnvironmentVariable(ModelEnvironment);
        if (!string.IsNullOrWhiteSpace(model))
            options.Model = model.Trim();

        options.RequestTimeoutSeconds = ReadInt(TimeoutEnvironment, options.RequestTimeoutSeconds);
        options.MaxRetries = ReadInt(MaxRetriesEnvironment, options.MaxRetries);

        return options;
    }

    private static int ReadInt(string name, int fallback)
    {
        string? raw = Environment.GetEnvironmentVariable(name);
        return int.TryParse(raw, out int value) && value >= 0 ? value : fallback;
    }
}

/// <summary>
/// Creates provider instances from <see cref="AIAgentOptions"/> plus per-node overrides.
/// </summary>
public sealed class AIAgentProviderFactory
{
    /// <summary>
    /// The process-wide default factory, created from environment variables.
    /// </summary>
    public static AIAgentProviderFactory Default { get; } = new(AIAgentOptions.FromEnvironment());

    /// <summary>
    /// Creates the factory for the given options.
    /// </summary>
    public AIAgentProviderFactory(AIAgentOptions options)
    {
        Options = options;
    }

    /// <summary>
    /// The options used when a node does not override them.
    /// </summary>
    public AIAgentOptions Options { get; }

    /// <summary>
    /// Creates a provider. Values passed here override the factory options.
    /// </summary>
    public IAgentProvider Create(
        AIProviderKind kind = AIProviderKind.Inherit,
        string? endpoint = null,
        string? model = null,
        int? timeoutSeconds = null,
        IReadOnlyList<string>? mockResponses = null)
    {
        AIProviderKind resolved = kind == AIProviderKind.Inherit ? Options.Provider : kind;
        if (resolved == AIProviderKind.Inherit)
            resolved = AIProviderKind.Ollama;

        return resolved switch
        {
            AIProviderKind.Mock => new MockAgentProvider(
                mockResponses is { Count: > 0 } ? mockResponses.ToArray() : Options.MockResponses),
            _ => new OllamaAgentProvider(
                endpoint ?? Options.Endpoint,
                model ?? Options.Model,
                timeoutSeconds ?? Options.RequestTimeoutSeconds),
        };
    }
}
