using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using SchemaNode.Http;
using SchemaNode.Schema;
using SchemaNode.Schema.Validation;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.AI.Chat;

/// <summary>
/// The kind of result returned by the schema assistant.
/// </summary>
public enum SchemaChatResultKind
{
    /// <summary>A plain natural language answer, no schema change was proposed.</summary>
    Chat,

    /// <summary>A validated schema change candidate that the editor can apply.</summary>
    SchemaChange,

    /// <summary>The assistant proposed a schema change, but server side validation failed.</summary>
    ValidationFailed,

    /// <summary>The assistant could not complete the request (model or tool error).</summary>
    Error,
}

/// <summary>
/// The request for <c>POST /schema/chat-schema</c> and <c>POST /schema/chat-schema-stream</c>.
/// </summary>
public class ChatSchemaRequest : SchemaApiRequest
{
    /// <summary>The natural language message from the user.</summary>
    [Required]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The conversation identifier returned by a previous response. Omit it to start a new
    /// conversation.
    /// </summary>
    public string? ConversationId { get; set; }

    /// <summary>
    /// The full name of the schema currently edited in the SchemaNode editor, e.g.
    /// <c>demo.address</c>. When set, a proposed change must keep this identity.
    /// </summary>
    public string? Target { get; set; }

    /// <summary>
    /// The current editor content. This is the authoritative content of the editor, including
    /// unsaved modifications. It is used only as model context; it is never treated as
    /// authorization evidence.
    /// </summary>
    public JsonNode? CurrentSchema { get; set; }

    /// <summary>Whether the editor is creating a new schema.</summary>
    public bool? IsNew { get; set; }
}

/// <summary>
/// The response of the schema assistant.
/// </summary>
public class ChatSchemaResponse : SchemaApiResponse
{
    /// <summary>The conversation identifier to send with the next message.</summary>
    public string ConversationId { get; set; } = string.Empty;

    /// <summary>The result kind.</summary>
    public SchemaChatResultKind Kind { get; set; }

    /// <summary>The natural language answer of the assistant.</summary>
    public string? Message { get; set; }

    /// <summary>The schema change candidate, set for <see cref="SchemaChatResultKind.SchemaChange"/> and <see cref="SchemaChatResultKind.ValidationFailed"/>.</summary>
    public SchemaChangeCandidate? Change { get; set; }

    /// <summary>The error code for <see cref="SchemaChatResultKind.Error"/>.</summary>
    public string? ErrorCode { get; set; }

    /// <summary>The MCP tools invoked by the agent (names only, no raw tool output).</summary>
    public string[]? ToolCalls { get; set; }
}

/// <summary>
/// A schema change candidate produced by the assistant.
/// <para>
/// The candidate targets the schema currently open in the editor. It is never persisted by the
/// assistant; the user must explicitly apply it and save it in the editor.
/// </para>
/// </summary>
public class SchemaChangeCandidate
{
    /// <summary><c>create</c> or <c>update</c>.</summary>
    public string Operation { get; set; } = SchemaCandidateValidator.OperationUpdate;

    /// <summary>The full name of the schema the candidate targets.</summary>
    public string TargetName { get; set; } = string.Empty;

    /// <summary>The proposed node schema data.</summary>
    public NodeSchema? Schema { get; set; }

    /// <summary>The normalized node schema JSON (safe to apply in the editor).</summary>
    public JsonNode? Normalized { get; set; }

    /// <summary>The assistant's short description of the change.</summary>
    public string? Summary { get; set; }

    /// <summary>Whether the server side validation passed.</summary>
    public bool IsValid { get; set; }

    /// <summary>The blocking validation problems.</summary>
    public SchemaValidationError[] Errors { get; set; } = [];

    /// <summary>Non blocking findings.</summary>
    public string[] Warnings { get; set; } = [];

    /// <summary>
    /// A backend generated list of changed JSON paths, e.g. <c>+struct.fields[email]</c>.
    /// Independent from the model's own summary.
    /// </summary>
    public string[] Changes { get; set; } = [];
}

/// <summary>
/// The compact result returned to the model by the proposal tool.
/// </summary>
public class SchemaProposalToolResult
{
    /// <summary>Whether the proposal passed validation.</summary>
    public bool IsValid { get; set; }

    /// <summary><c>create</c> or <c>update</c>.</summary>
    public string? Operation { get; set; }

    /// <summary>The candidate full name.</summary>
    public string? TargetName { get; set; }

    /// <summary>The blocking validation problems.</summary>
    public SchemaValidationError[] Errors { get; set; } = [];

    /// <summary>Non blocking findings.</summary>
    public string[] Warnings { get; set; } = [];

    /// <summary>A retry hint for the model when the proposal is invalid.</summary>
    public string? Hint { get; set; }
}

/// <summary>
/// A streaming update produced by the schema assistant.
/// </summary>
public abstract record SchemaChatUpdate;

/// <summary>The conversation was resolved; carries the identifier for the client.</summary>
public sealed record SchemaChatStarted(string ConversationId) : SchemaChatUpdate;

/// <summary>A chunk of streamed assistant text.</summary>
public sealed record SchemaChatTextDelta(string Text) : SchemaChatUpdate;

/// <summary>The final assistant response.</summary>
public sealed record SchemaChatCompleted(ChatSchemaResponse Response) : SchemaChatUpdate;

/// <summary>The assistant failed before producing a response.</summary>
public sealed record SchemaChatFailed(string Code, string Message) : SchemaChatUpdate;
