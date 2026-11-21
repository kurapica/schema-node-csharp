using System.Text.Json;
using System.Text.Json.Nodes;

namespace SchemaNode.AI.Chat;

/// <summary>
/// Maps between the transport agnostic <see cref="IAIChat"/> contract and the existing schema
/// assistant wire protocol. Prompt assembly stays in <see cref="SchemaChatService"/>; this mapper
/// only moves data.
/// </summary>
internal static class AIChatMapper
{
    /// <summary>Converts a contract request into the wire request.</summary>
    public static ChatSchemaRequest ToWire(AIChatRequest request)
    {
        return new ChatSchemaRequest
        {
            Message = request.UserContent,
            ConversationId = request.ConversationId,
            Target = request.Target?.Name,
            CurrentSchema = request.Target?.Schema?.DeepClone(),
            IsNew = request.Target?.IsNew,
            Locale = request.Locale,
            SystemContent = request.SystemContent,
            ValidationFeedback = request.ValidationFeedback,
            History = request.History.Count == 0 ? null : request.History.ToArray(),
            ModelId = request.Options.Model,
            Temperature = request.Options.Temperature,
            MaxTokens = request.Options.MaxTokens,
            TimeoutSeconds = request.Options.TimeoutSeconds,
        };
    }

    /// <summary>Converts a wire response into the contract result.</summary>
    public static AIChatResult ToResult(ChatSchemaResponse response)
    {
        SchemaChangeCandidate? change = response.Change;

        // The normalized JSON is the value the editor can apply directly. When the backend did not
        // normalize it (for example an invalid candidate), fall back to the typed schema.
        JsonNode? schema = change?.Normalized?.DeepClone();
        if (schema is null && change?.Schema is not null)
            schema = JsonSerializer.SerializeToNode(change.Schema, SchemaChatJson.Options);

        return new AIChatResult
        {
            ConversationId = response.ConversationId,
            Kind = response.Kind switch
            {
                SchemaChatResultKind.SchemaChange => AIChatResultKind.SchemaChange,
                SchemaChatResultKind.ValidationFailed => AIChatResultKind.ValidationFailed,
                SchemaChatResultKind.Error => AIChatResultKind.Error,
                _ => AIChatResultKind.Chat,
            },
            Message = response.Message,
            Schema = schema,
            Change = change,
            Errors = change?.Errors ?? [],
            Warnings = change?.Warnings ?? [],
            Changes = change?.Changes ?? [],
            ToolCalls = response.ToolCalls ?? [],
            ErrorCode = response.ErrorCode,
        };
    }
}
