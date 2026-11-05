using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Property.Core;

namespace SchemaNode.AI.Agents;

/// <summary>
/// The provider used to generate a completion for the AI workflow node.
/// </summary>
public interface IAgentProvider
{
    /// <summary>
    /// The provider name shown in diagnostics.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Generates one completion for the request.
    /// </summary>
    Task<string> CompleteAsync(AIAgentRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// The data sent to an <see cref="IAgentProvider"/>.
/// </summary>
public sealed class AIAgentRequest
{
    /// <summary>
    /// The system instruction, including the target schema description.
    /// </summary>
    public required string SystemPrompt { get; init; }

    /// <summary>
    /// The user instruction, including the configured prompt sources and any retry feedback.
    /// </summary>
    public required string UserPrompt { get; init; }

    /// <summary>
    /// The sampling temperature, when the provider supports it.
    /// </summary>
    public double? Temperature { get; init; }

    /// <summary>
    /// The maximum number of tokens to generate, when the provider supports it.
    /// </summary>
    public int? MaxTokens { get; init; }
}

/// <summary>
/// Raised when an agent provider is unreachable or returns an unusable transport response.
/// Validation failures are deliberately not reported with this exception: they are retried
/// inside the workflow node.
/// </summary>
public sealed class AIAgentException : Exception
{
    /// <summary>
    /// Creates the exception for the given provider.
    /// </summary>
    public AIAgentException(string provider, string message, Exception? innerException = null)
        : base($"[{provider}] {message}", innerException)
    {
        Provider = provider;
    }

    /// <summary>
    /// The provider that failed.
    /// </summary>
    public string Provider { get; }
}

/// <summary>
/// Selects the agent provider for the AI workflow node.
/// </summary>
[Meta<SchemaType>("system.workflow.ai.provider")]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AIProviderKind
{
    /// <summary>Use the configured default provider.</summary>
    Inherit,

    /// <summary>Deterministic offline provider, primarily intended for tests and manual dry runs.</summary>
    Mock,

    /// <summary>Local Ollama server, <c>/api/chat</c>.</summary>
    Ollama,
}
