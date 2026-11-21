using System.Text.Json.Nodes;
using SchemaNode.Schema.Validation;

namespace SchemaNode.AI.Chat;

/// <summary>
/// The transport agnostic schema assistant contract.
/// <para>
/// A local host resolves an in-process implementation; a remote host can satisfy the same contract
/// with an HTTP client. Callers only deal with conversation content and the final structured
/// result, never with a provider SDK type.
/// </para>
/// </summary>
public interface IAIChat
{
    /// <summary>
    /// Runs one conversation turn and streams the assistant updates, ending with
    /// <see cref="AIChatCompleted"/> when the turn produced a result.
    /// </summary>
    /// <param name="request">The turn request.</param>
    /// <param name="cancellationToken">Cancels the turn.</param>
    IAsyncEnumerable<AIChatUpdate> ChatAsync(AIChatRequest request, CancellationToken cancellationToken = default);
}

/// <summary>The role of a conversation turn.</summary>
public enum AIChatRole
{
    /// <summary>A system instruction.</summary>
    System,

    /// <summary>A user turn.</summary>
    User,

    /// <summary>An assistant turn.</summary>
    Assistant,
}

/// <summary>One conversation turn supplied by the caller.</summary>
public sealed record AIChatTurn(AIChatRole Role, string Content);

/// <summary>The schema the editor currently shows, used as assistant context.</summary>
public sealed class AIChatTarget
{
    /// <summary>The full name of the edited schema, e.g. <c>demo.address</c>.</summary>
    public string? Name { get; init; }

    /// <summary>The current editor content, including unsaved modifications.</summary>
    public JsonNode? Schema { get; init; }

    /// <summary>Whether the editor is creating a new schema.</summary>
    public bool? IsNew { get; init; }
}

/// <summary>The per-turn generation knobs. Null values fall back to host configuration.</summary>
public sealed class AIChatGenerationOptions
{
    /// <summary>The model identifier.</summary>
    public string? Model { get; init; }

    /// <summary>The sampling temperature.</summary>
    public float? Temperature { get; init; }

    /// <summary>The maximum number of output tokens.</summary>
    public int? MaxTokens { get; init; }

    /// <summary>The timeout in seconds for the turn.</summary>
    public int? TimeoutSeconds { get; init; }
}

/// <summary>One conversation turn going in.</summary>
public sealed class AIChatRequest
{
    /// <summary>The user message.</summary>
    public string UserContent { get; init; } = string.Empty;

    /// <summary>An optional extra system instruction appended to the built prompt.</summary>
    public string? SystemContent { get; init; }

    /// <summary>The conversation identifier returned by a previous turn.</summary>
    public string? ConversationId { get; init; }

    /// <summary>
    /// Optional turns for a stateless caller. Used only when no conversation identifier is supplied.
    /// </summary>
    public IReadOnlyList<AIChatTurn> History { get; init; } = [];

    /// <summary>The current editor context.</summary>
    public AIChatTarget? Target { get; init; }

    /// <summary>The per-turn generation knobs.</summary>
    public AIChatGenerationOptions Options { get; init; } = new();

    /// <summary>
    /// Concrete validation errors from the previous attempt. The assistant is asked to correct them.
    /// </summary>
    public string? ValidationFeedback { get; init; }

    /// <summary>The UI locale used for localized assistant text.</summary>
    public string? Locale { get; init; }

    /// <summary>Whether text deltas should be streamed. Implementations may ignore this flag.</summary>
    public bool Stream { get; init; }
}

/// <summary>The kind of result returned by the assistant.</summary>
public enum AIChatResultKind
{
    /// <summary>A plain natural language answer.</summary>
    Chat,

    /// <summary>A validated schema change candidate.</summary>
    SchemaChange,

    /// <summary>A proposed schema change that failed validation.</summary>
    ValidationFailed,

    /// <summary>The assistant could not complete the request.</summary>
    Error,
}

/// <summary>One conversation turn going out.</summary>
public sealed class AIChatResult
{
    /// <summary>The conversation identifier to send with the next turn.</summary>
    public string ConversationId { get; init; } = string.Empty;

    /// <summary>The result kind.</summary>
    public AIChatResultKind Kind { get; init; }

    /// <summary>The natural language answer.</summary>
    public string? Message { get; init; }

    /// <summary>
    /// The complete normalized NodeSchema payload, suitable for a whole-node write-back.
    /// </summary>
    public JsonNode? Schema { get; init; }

    /// <summary>The full change candidate, kept for hosts that want to preview the diff.</summary>
    public SchemaChangeCandidate? Change { get; init; }

    /// <summary>The blocking validation problems.</summary>
    public SchemaValidationError[] Errors { get; init; } = [];

    /// <summary>Non blocking validation findings.</summary>
    public string[] Warnings { get; init; } = [];

    /// <summary>The backend computed changed JSON paths.</summary>
    public string[] Changes { get; init; } = [];

    /// <summary>The tools invoked by the assistant (names only).</summary>
    public string[] ToolCalls { get; init; } = [];

    /// <summary>The error code for <see cref="AIChatResultKind.Error"/>.</summary>
    public string? ErrorCode { get; init; }
}

/// <summary>A streaming update produced by the assistant.</summary>
public abstract record AIChatUpdate;

/// <summary>The conversation was resolved.</summary>
public sealed record AIChatStarted(string ConversationId) : AIChatUpdate;

/// <summary>A chunk of streamed assistant text.</summary>
public sealed record AIChatDelta(string Text) : AIChatUpdate;

/// <summary>The final assistant result.</summary>
public sealed record AIChatCompleted(AIChatResult Result) : AIChatUpdate;

/// <summary>The assistant failed before producing a result.</summary>
public sealed record AIChatFailed(string Code, string Message) : AIChatUpdate;
