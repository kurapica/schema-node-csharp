using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SchemaNode.AI.Agents;

/// <summary>
/// Provider for a local Ollama server using <c>POST /api/chat</c> with a non-streaming,
/// JSON-formatted response. Every request has an explicit timeout so a missing or slow server
/// never blocks the workflow indefinitely.
/// </summary>
public sealed class OllamaAgentProvider : IAgentProvider
{
    private static readonly HttpClient SharedClient = new()
    {
        // The timeout is enforced per request with a linked cancellation token below.
        Timeout = Timeout.InfiniteTimeSpan,
    };

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;
    private readonly Uri _chatUri;
    private readonly string _model;
    private readonly int _timeoutSeconds;

    /// <summary>
    /// Creates the provider.
    /// </summary>
    /// <param name="endpoint">Base URL such as <c>http://localhost:11434</c> or the full <c>/api/chat</c> URL.</param>
    /// <param name="model">Ollama model name.</param>
    /// <param name="timeoutSeconds">Per-request timeout in seconds.</param>
    /// <param name="client">Optional HTTP client, mainly for tests.</param>
    public OllamaAgentProvider(string endpoint, string model, int timeoutSeconds, HttpClient? client = null)
    {
        _model = string.IsNullOrWhiteSpace(model) ? "qwen3:8b" : model.Trim();
        _timeoutSeconds = timeoutSeconds <= 0 ? 120 : timeoutSeconds;
        _client = client ?? SharedClient;

        string normalized = string.IsNullOrWhiteSpace(endpoint)
            ? "http://localhost:11434"
            : endpoint.Trim().TrimEnd('/');
        if (!normalized.EndsWith("/api/chat", StringComparison.OrdinalIgnoreCase))
            normalized += "/api/chat";

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out Uri? uri))
            throw new AIAgentException("ollama", $"Invalid Ollama endpoint: '{endpoint}'.");

        _chatUri = uri;
    }

    /// <inheritdoc />
    public string Name => "ollama";

    /// <inheritdoc />
    public async Task<string> CompleteAsync(AIAgentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string payload = JsonSerializer.Serialize(new
        {
            model = _model,
            stream = false,
            format = "json",
            // qwen3 spends most of a small token budget on reasoning before emitting content.
            // JSON generation is a structured task, so thinking is disabled to keep latency low.
            think = false,
            options = new
            {
                temperature = request.Temperature ?? 0.2,
                num_predict = Math.Max(1, request.MaxTokens ?? 512),
            },
            messages = new object[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user", content = request.UserPrompt },
            },
        }, SerializerOptions);

        using StringContent content = new(payload, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));

        HttpResponseMessage response;
        try
        {
            response = await _client.PostAsync(_chatUri, content, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AIAgentException(
                "ollama",
                $"The request to {_chatUri} timed out after {_timeoutSeconds}s. The model may still be loading; " +
                "start Ollama, pull the model, or increase the timeout.");
        }
        catch (HttpRequestException ex)
        {
            throw new AIAgentException(
                "ollama",
                $"Ollama is not reachable at {_chatUri}. Start it with 'ollama serve' and make sure model '{_model}' is pulled.",
                ex);
        }

        using (response)
        {
            string body = await ReadBodyAsync(response, timeoutCts.Token, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                string detail = Truncate(body, 400);
                throw new AIAgentException(
                    "ollama",
                    $"Ollama returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}) for model '{_model}'. {detail}".Trim());
            }

            try
            {
                JsonNode? json = JsonNode.Parse(body);
                if (json?["error"] is JsonValue error)
                    throw new AIAgentException("ollama", $"Ollama reported an error: {error.GetValue<string>()}");

                string? text = json?["message"]?["content"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(text))
                    throw new AIAgentException("ollama", $"Ollama returned an empty completion for model '{_model}'. Response: {Truncate(body, 400)}");

                return text;
            }
            catch (JsonException ex)
            {
                throw new AIAgentException("ollama", $"Ollama returned an unreadable response: {Truncate(body, 400)}", ex);
            }
        }
    }

    private static async Task<string> ReadBodyAsync(
        HttpResponseMessage response,
        CancellationToken timeoutToken,
        CancellationToken callerToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(timeoutToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!callerToken.IsCancellationRequested)
        {
            throw new AIAgentException("ollama", "Timed out while reading the Ollama response.");
        }
    }

    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= max ? value : value[..max] + "...";
    }
}
