using System.Text.Json.Serialization;
using SchemaNode.AI.Agents;
using SchemaNode.Attribute;
using SchemaNode.Property.Core;

namespace SchemaNode.AI.Workflow;

/// <summary>
/// The AI workflow node settings. The class is generated as a struct schema, so the Man
/// workflow editor renders it through the normal <c>State</c> editor without special casing.
/// </summary>
[Meta<SchemaType>("system.workflow.ai.settings")]
public sealed class AIWorkflowSettings
{
    /// <summary>
    /// Optional system instruction prepended to the built-in JSON-generation instruction.
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// Optional task instruction appended to the prompt sources.
    /// </summary>
    public string? Instruction { get; set; }

    /// <summary>
    /// The ordered prompt sources. Each source can be literal text, the node's bound context
    /// object, or the payload of an upstream workflow node.
    /// </summary>
    public AIWorkflowPromptSource[]? Prompts { get; set; }

    /// <summary>
    /// The provider for this node. <see cref="AIProviderKind.Inherit"/> uses the registered default.
    /// </summary>
    public AIProviderKind Provider { get; set; } = AIProviderKind.Inherit;

    /// <summary>
    /// Optional Ollama base URL override.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Optional Ollama model override.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Additional generation attempts after a validation failure. Defaults to the provider options.
    /// </summary>
    public int? MaxRetries { get; set; }

    /// <summary>
    /// Optional per-request timeout in seconds.
    /// </summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// Optional completion token budget.
    /// </summary>
    public int? MaxTokens { get; set; }

    /// <summary>
    /// Optional sampling temperature.
    /// </summary>
    public double? Temperature { get; set; }

    /// <summary>
    /// Optional cap for the target schema description injected into the prompt.
    /// </summary>
    public int? MaxMetadataChars { get; set; }

    /// <summary>
    /// Newline separated deterministic responses used when <see cref="Provider"/> is
    /// <see cref="AIProviderKind.Mock"/>. The first line is the first attempt, the second line
    /// the first retry, and so on. The last line is repeated when exhausted.
    /// </summary>
    public string? MockResponses { get; set; }
}

/// <summary>
/// The workflow session state for the AI node. It records the number of model calls and the
/// last validation error, which is useful when a workflow is inspected after a failure.
/// </summary>
[Meta<SchemaType>("system.workflow.ai.session")]
public sealed class AIWorkflowSession
{
    /// <summary>The number of model calls made by this node.</summary>
    public int Attempts { get; set; }

    /// <summary>The last validation error, null after a successful run.</summary>
    public string? LastError { get; set; }
}

/// <summary>
/// One prompt source. The source is rendered as a labeled block in the user prompt.
/// </summary>
[Meta<SchemaType>("system.workflow.ai.prompt")]
public sealed class AIWorkflowPromptSource
{
    /// <summary>The kind of the source.</summary>
    public AIWorkflowPromptSourceKind Kind { get; set; } = AIWorkflowPromptSourceKind.Text;

    /// <summary>Optional label shown before the block.</summary>
    public string? Label { get; set; }

    /// <summary>Literal text for <see cref="AIWorkflowPromptSourceKind.Text"/>.</summary>
    public string? Text { get; set; }

    /// <summary>
    /// An upstream workflow node name for <see cref="AIWorkflowPromptSourceKind.Payload"/>.
    /// The node's payload is serialized as JSON.
    /// </summary>
    public string? Node { get; set; }

    /// <summary>
    /// An optional payload access path (for example <c>max</c>) for
    /// <see cref="AIWorkflowPromptSourceKind.Payload"/>.
    /// </summary>
    public string? Path { get; set; }
}

/// <summary>
/// The kind of an <see cref="AIWorkflowPromptSource"/>.
/// </summary>
[Meta<SchemaType>("system.workflow.ai.prompt-source")]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AIWorkflowPromptSourceKind
{
    /// <summary>Literal text.</summary>
    Text,

    /// <summary>The current workflow context object bound to the node's <c>input</c> argument.</summary>
    Context,

    /// <summary>The payload of an upstream workflow node, optionally narrowed by a path.</summary>
    Payload,
}
