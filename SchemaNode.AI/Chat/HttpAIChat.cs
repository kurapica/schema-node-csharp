using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace SchemaNode.AI.Chat;

/// <summary>
/// Configuration for the HTTP <see cref="IAIChat"/> implementation.
/// </summary>
public sealed class AIChatWebApiOptions
{
    /// <summary>
    /// The schema API base address, including the schema prefix, e.g.
    /// <c>http://localhost:5000/schema</c>.
    /// </summary>
    public string BaseAddress { get; set; } = string.Empty;

    /// <summary>The non streaming assistant path relative to <see cref="BaseAddress"/>.</summary>
    public string ChatPath { get; set; } = "chat-schema";
}

/// <summary>
/// The remote <see cref="IAIChat"/> implementation. It calls the schema assistant HTTP endpoint so
/// a host that does not own a <see cref="SchemaContext"/> can still consume the same contract.
/// </summary>
public sealed class HttpAIChat : IAIChat
{
    private readonly HttpClient _client;
    private readonly AIChatWebApiOptions _options;
    private readonly Uri _endpoint;

    /// <summary>Creates the HTTP implementation.</summary>
    public HttpAIChat(HttpClient client, AIChatWebApiOptions options)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(options.BaseAddress))
            throw new InvalidOperationException("The schema assistant base address is required.");

        _endpoint = new Uri(
            options.BaseAddress.TrimEnd('/') + "/" + options.ChatPath.TrimStart('/'));
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<AIChatUpdate> ChatAsync(
        AIChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        AIChatResult result = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        yield return new AIChatStarted(result.ConversationId);
        yield return new AIChatCompleted(result);
    }

    private async Task<AIChatResult> SendAsync(AIChatRequest request, CancellationToken cancellationToken)
    {
        try
        {
            string body = JsonSerializer.Serialize(AIChatMapper.ToWire(request), SchemaChatJson.Options);
            using HttpRequestMessage message = new(HttpMethod.Post, _endpoint)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };

            using HttpResponseMessage response =
                await _client.SendAsync(message, cancellationToken).ConfigureAwait(false);

            string payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Error(request, "http_error",
                    ExtractMessage(payload) ?? $"The assistant request failed ({response.StatusCode}).");
            }

            ChatSchemaResponse? wire = JsonSerializer.Deserialize<ChatSchemaResponse>(
                payload, SchemaChatJson.Options);
            if (wire is null)
                return Error(request, "invalid_response", "The assistant returned an empty response.");

            return AIChatMapper.ToResult(wire);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Error(request, "http_error", ex.Message);
        }
    }

    private static AIChatResult Error(AIChatRequest request, string code, string message)
    {
        return new AIChatResult
        {
            ConversationId = request.ConversationId ?? string.Empty,
            Kind = AIChatResultKind.Error,
            ErrorCode = code,
            Message = message,
        };
    }

    private static string? ExtractMessage(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return null;
        try
        {
            using JsonDocument document = JsonDocument.Parse(payload);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            foreach (string name in new[] { "message", "error" })
            {
                if (root.TryGetProperty(name, out JsonElement value)
                    && value.ValueKind == JsonValueKind.String)
                    return value.GetString();
            }
        }
        catch (JsonException)
        {
            // Not a JSON error payload.
        }
        return null;
    }
}
