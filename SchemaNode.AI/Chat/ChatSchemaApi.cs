using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchemaNode.Http;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.AI.Chat;

/// <summary>
/// The non streaming schema assistant API, exposed as <c>POST /schema/chat-schema</c>.
/// <para>
/// The endpoint reuses the existing schema API protocol (locale, time zone, error handling and
/// authorization middleware); the streaming variant is mapped by
/// <see cref="SchemaNodeChatInjection.MapSchemaChat"/>.
/// </para>
/// </summary>
public class ChatSchemaApi : SchemaApi<ChatSchemaRequest, ChatSchemaResponse>
{
    /// <inheritdoc />
    protected override async Task<ChatSchemaResponse?> ExecuteAsync(
        ChatSchemaRequest request, CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]ChatSchema [Request]{request}", request);

        string owner = request.Context?.User?.Identity?.Name ?? "anonymous";
        SchemaChatService service = Services.GetRequiredService<SchemaChatService>();

        await foreach (SchemaChatUpdate update in service.ExecuteAsync(request, owner, cancellationToken))
        {
            if (update is SchemaChatCompleted completed)
                return completed.Response;
            if (update is SchemaChatFailed failed)
                return new ChatSchemaResponse
                {
                    Kind = SchemaChatResultKind.Error,
                    ErrorCode = failed.Code,
                    Message = failed.Message,
                };
        }

        return new ChatSchemaResponse
        {
            Kind = SchemaChatResultKind.Error,
            ErrorCode = "empty_response",
            Message = "The assistant returned no answer.",
        };
    }
}
