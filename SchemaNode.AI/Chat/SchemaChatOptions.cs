namespace SchemaNode.AI.Chat;

/// <summary>
/// The chat completion back-end used by the schema assistant.
/// </summary>
public enum SchemaChatProvider
{
    /// <summary>OpenAI hosted API, or any OpenAI compatible server when an endpoint is configured.</summary>
    OpenAI,

    /// <summary>Azure OpenAI Service.</summary>
    AzureOpenAI,

    /// <summary>Local Ollama server through its OpenAI compatible <c>/v1</c> endpoint.</summary>
    Ollama,
}

/// <summary>
/// Configuration for the schema assistant chat endpoint.
/// Register via <see cref="SchemaNodeChatInjection.AddSchemaChat"/>.
/// </summary>
public class SchemaChatOptions
{
    /// <summary>Default configuration section name in <c>appsettings.json</c>.</summary>
    public const string SectionName = "SchemaChat";

    /// <summary>The chat completion provider.</summary>
    public SchemaChatProvider Provider { get; set; } = SchemaChatProvider.OpenAI;

    /// <summary>The model identifier (or Azure deployment name).</summary>
    public string ModelId { get; set; } = "gpt-4.1-mini";

    /// <summary>The provider API key. Not used by <see cref="SchemaChatProvider.Ollama"/>.</summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Optional endpoint.
    /// <list type="bullet">
    ///   <item><b>OpenAI</b>: overrides the default <c>https://api.openai.com/v1</c> endpoint.</item>
    ///   <item><b>AzureOpenAI</b>: required resource endpoint.</item>
    ///   <item><b>Ollama</b>: base URL, defaults to <c>http://localhost:11434</c>.</item>
    /// </list>
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>Azure deployment name. Falls back to <see cref="ModelId"/>.</summary>
    public string? DeploymentName { get; set; }

    /// <summary>Maximum number of previous chat messages sent back to the model.</summary>
    public int MaxHistoryMessages { get; set; } = 20;

    /// <summary>Maximum number of conversations kept in memory.</summary>
    public int MaxConversations { get; set; } = 500;

    /// <summary>Conversation idle timeout in minutes.</summary>
    public int ConversationTimeoutMinutes { get; set; } = 60;

    /// <summary>Maximum number of tool invocation rounds for one user message.</summary>
    public int MaxIterationsPerRequest { get; set; } = 8;

    /// <summary>Maximum time in seconds for one assistant turn. Zero disables the timeout.</summary>
    public int RequestTimeoutSeconds { get; set; } = 120;

    /// <summary>Maximum accepted user message length.</summary>
    public int MaxMessageChars { get; set; } = 8000;

    /// <summary>Maximum accepted current schema JSON length.</summary>
    public int MaxContextSchemaChars { get; set; } = 200_000;

    /// <summary>
    /// Maximum number of current schema characters embedded in the prompt. Larger schemas are
    /// truncated with an explicit note; the agent can still inspect the full schema with tools.
    /// </summary>
    public int MaxPromptSchemaChars { get; set; } = 40_000;

    /// <summary>The sampling temperature.</summary>
    public float? Temperature { get; set; } = 0.2f;
    /// <summary>Whether the streaming endpoint is enabled.</summary>
    public bool EnableStreaming { get; set; } = true;
}
