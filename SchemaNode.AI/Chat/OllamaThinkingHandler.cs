using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;

namespace SchemaNode.AI.Chat;

/// <summary>
/// Adds <c>"reasoning_effort": "none"</c> to Ollama's OpenAI compatible chat completion requests.
/// <para>
/// Ollama maps this field to its native <c>think: false</c> switch. Without it, qwen3 spends the
/// output budget on reasoning and can return an empty content string. The policy only touches
/// <c>POST .../chat/completions</c> calls, so unrelated requests are passed through unchanged.
/// </para>
/// </summary>
internal sealed class OllamaThinkingHandler : DelegatingHandler
{
    private const string ReasoningEffort = "none";

    /// <summary>Creates the handler.</summary>
    public OllamaThinkingHandler(HttpMessageHandler innerHandler) : base(innerHandler)
    {
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.Method == HttpMethod.Post
            && request.RequestUri is { AbsolutePath: var path }
            && path.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)
            && request.Content is not null)
        {
            string json = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            // The OpenAI SDK has already consumed the content; always replace it, even when the
            // body is unchanged. The body itself stays the same apart from the added field.
            request.Content = new StringContent(
                TryAddReasoningEffort(json, out string updated) ? updated : json,
                Encoding.UTF8,
                "application/json");
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static bool TryAddReasoningEffort(string json, out string updated)
    {
        updated = json;
        try
        {
            if (JsonNode.Parse(json) is not JsonObject body || body.ContainsKey("reasoning_effort"))
                return false;

            body["reasoning_effort"] = ReasoningEffort;
            updated = body.ToJsonString();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
