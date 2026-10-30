using System.ClientModel;
using System.Reflection;
using System.Text.Json;
using Azure.AI.OpenAI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using OpenAI;
using SchemaNode.AI.Chat;
using SchemaNode.Context;
using SchemaNode.Service;
using SchemaNode.Utility;

namespace SchemaNode.AI;

/// <summary>
/// Dependency-injection and endpoint extensions for the schema assistant of <c>SchemaNode.AI</c>.
/// </summary>
public static class SchemaNodeChatInjection
{
    /// <summary>
    /// Registers the schema assistant services and the <c>SchemaNode.AI</c> assembly for API
    /// discovery.
    /// <para>
    /// Call this <b>before</b> <c>PrepareSchemaRuntime</c> so the chat APIs are discovered.
    /// </para>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">
    /// Optional configuration, typically bound from <c>appsettings.json</c>:
    /// <code>opts => configuration.GetSection(SchemaChatOptions.SectionName).Bind(opts)</code>
    /// </param>
    public static IServiceCollection AddSchemaChat(
        this IServiceCollection services,
        Action<SchemaChatOptions>? configure = null)
    {
        var options = new SchemaChatOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        services.AddSingleton<SchemaChatConversationStore>();
        services.AddScoped<SchemaChatSession>();
        services.AddScoped<SchemaChatService>();

        // Allow tests or hosts to provide their own IChatClient; otherwise build the provider
        // specific client lazily. The provider is not contacted until the first chat request.
        services.TryAddSingleton<IChatClient>(_ => CreateChatClient(options));

        return services.AddAppSchemaAssemblies(Assembly.GetExecutingAssembly());
    }

    /// <summary>
    /// Maps the streaming schema assistant endpoint:
    /// <c>POST {prefix}/chat-schema-stream</c>, using server-sent events.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <param name="prefix">The schema API prefix, matching <c>UseSchemaApis(prefix: ...)</c>.</param>
    /// <param name="policy">
    /// Optional host authorization policy, matching the policy used for the schema management UI.
    /// Schema level create/update/read policies are always enforced by the tools and validator.
    /// </param>
    public static WebApplication MapSchemaChat(this WebApplication app, string prefix = "/schema", string? policy = null)
    {
        string path = $"{prefix.TrimEnd('/')}/chat-schema-stream";
        var endpoint = app.MapPost(path, HandleStreamAsync);
        if (!string.IsNullOrWhiteSpace(policy))
            endpoint.RequireAuthorization(policy);
        return app;
    }

    private static readonly JsonSerializerOptions StreamJsonOptions = new(SchemaChatJson.Options)
    {
        PropertyNameCaseInsensitive = true,
    };

    private static async Task HandleStreamAsync(HttpContext httpContext)
    {
        CancellationToken cancellationToken = httpContext.RequestAborted;
        if (httpContext.Request.ContentLength is > 2 * 1024 * 1024)
        {
            httpContext.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            await httpContext.Response.WriteAsJsonAsync(new
            {
                error = "payload_too_large",
                message = "The request body is too large.",
            }, cancellationToken);
            return;
        }

        ChatSchemaRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<ChatSchemaRequest>(
                httpContext.Request.Body, StreamJsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsJsonAsync(new
            {
                error = "invalid_request",
                message = ex.Message,
            }, cancellationToken);
            return;
        }

        if (request == null || string.IsNullOrWhiteSpace(request.Message))
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsJsonAsync(new
            {
                error = "invalid_request",
                message = "The message is required.",
            }, cancellationToken);
            return;
        }

        SchemaChatOptions options = httpContext.RequestServices.GetRequiredService<SchemaChatOptions>();
        if (!options.EnableStreaming)
        {
            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        SchemaContext schemaContext = httpContext.RequestServices.GetRequiredService<SchemaContext>();
        if (!string.IsNullOrWhiteSpace(request.Locale))
            schemaContext.SetLocale(request.Locale);

        SchemaChatService service = httpContext.RequestServices.GetRequiredService<SchemaChatService>();
        ILogger logger = httpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("SchemaNode.AI.Chat.Stream");

        httpContext.Response.StatusCode = StatusCodes.Status200OK;
        httpContext.Response.ContentType = "text/event-stream; charset=utf-8";
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers.Connection = "keep-alive";
        httpContext.Response.Headers["X-Accel-Buffering"] = "no";
        httpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        string owner = httpContext.User?.Identity?.Name ?? "anonymous";
        bool wroteEvent = false;

        try
        {
            await foreach (SchemaChatUpdate update in service.ExecuteStreamAsync(request, owner, cancellationToken))
            {
                switch (update)
                {
                    case SchemaChatStarted started:
                        await WriteEventAsync(httpContext.Response, "meta",
                            JsonSerializer.Serialize(new { conversationId = started.ConversationId }, SchemaChatJson.Options),
                            cancellationToken);
                        wroteEvent = true;
                        break;
                    case SchemaChatTextDelta delta:
                        await WriteEventAsync(httpContext.Response, "delta",
                            JsonSerializer.Serialize(new { text = delta.Text }, SchemaChatJson.Options),
                            cancellationToken);
                        wroteEvent = true;
                        break;
                    case SchemaChatCompleted completed:
                        await WriteEventAsync(httpContext.Response, "result",
                            JsonSerializer.Serialize(completed.Response, SchemaChatJson.Options),
                            cancellationToken);
                        wroteEvent = true;
                        break;
                    case SchemaChatFailed failed:
                        await WriteEventAsync(httpContext.Response, "error",
                            JsonSerializer.Serialize(new { code = failed.Code, message = failed.Message }, SchemaChatJson.Options),
                            cancellationToken);
                        wroteEvent = true;
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The client closed the connection; nothing to write.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Chat] The streaming endpoint failed.");
            if (!wroteEvent)
            {
                httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await httpContext.Response.WriteAsJsonAsync(new
                {
                    error = "agent_error",
                    message = ex.Message,
                }, CancellationToken.None);
            }
            else
            {
                await WriteEventAsync(httpContext.Response, "error",
                    JsonSerializer.Serialize(new { code = "agent_error", message = ex.Message }, SchemaChatJson.Options),
                    CancellationToken.None);
            }
        }
    }

    private static async Task WriteEventAsync(
        HttpResponse response,
        string name,
        string json,
        CancellationToken cancellationToken)
    {
        await response.WriteAsync($"event: {name}\ndata: {json}\n\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }

    private static IChatClient CreateChatClient(SchemaChatOptions options)
    {
        switch (options.Provider)
        {
            case SchemaChatProvider.OpenAI:
            {
                if (string.IsNullOrWhiteSpace(options.ApiKey))
                    throw new InvalidOperationException(
                        $"SchemaChat is not configured: {SchemaChatOptions.SectionName}:ApiKey is required for the OpenAI provider.");

                OpenAIClient client = string.IsNullOrWhiteSpace(options.Endpoint)
                    ? new OpenAIClient(new ApiKeyCredential(options.ApiKey))
                    : new OpenAIClient(new ApiKeyCredential(options.ApiKey),
                        new OpenAIClientOptions { Endpoint = new Uri(options.Endpoint) });

                return client.GetChatClient(options.ModelId).AsIChatClient();
            }

            case SchemaChatProvider.AzureOpenAI:
            {
                if (string.IsNullOrWhiteSpace(options.Endpoint) || string.IsNullOrWhiteSpace(options.ApiKey))
                    throw new InvalidOperationException(
                        $"SchemaChat is not configured: {SchemaChatOptions.SectionName}:Endpoint and :ApiKey are required for the Azure OpenAI provider.");

                AzureOpenAIClient client = new(
                    new Uri(options.Endpoint),
                    new ApiKeyCredential(options.ApiKey));

                return client.GetChatClient(options.DeploymentName ?? options.ModelId).AsIChatClient();
            }

            case SchemaChatProvider.Ollama:
            {
                string baseUrl = string.IsNullOrWhiteSpace(options.Endpoint)
                    ? "http://localhost:11434"
                    : options.Endpoint.TrimEnd('/');
                if (!baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                    baseUrl += "/v1";

                // Ollama ignores the API key but the OpenAI SDK requires a non empty credential.
                OpenAIClient client = new(
                    new ApiKeyCredential("ollama"),
                    new OpenAIClientOptions { Endpoint = new Uri(baseUrl) });

                return client.GetChatClient(options.ModelId).AsIChatClient();
            }

            default:
                throw new InvalidOperationException($"Unsupported schema chat provider: {options.Provider}");
        }
    }
}
