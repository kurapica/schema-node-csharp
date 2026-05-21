using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SchemaNode.App.Http;

/// <summary>
/// Base class for App-layer API handlers, mirroring the Core SchemaApi pattern.
/// Subclass this and register via DI / AddAppSchemaNode.
/// </summary>
public abstract class AppApi<TRequest, TResponse>
    where TRequest : AppApiRequest
    where TResponse : AppApiResponse
{
    /// <summary>
    /// Called by the framework dispatcher. Do not call directly.
    /// </summary>
    public async Task<TResponse?> _ExecuteAsync(TRequest request, ILogger logger)
    {
        HttpContext http = request.HttpContext!;
        Services   = http.RequestServices;
        Logger     = logger;
        AppContext = Services.GetRequiredService<IAppSchemaContext>();
        return await ExecuteAsync(request, http.RequestAborted);
    }

    /// <summary>Override to implement the API logic.</summary>
    protected virtual Task<TResponse?> ExecuteAsync(TRequest request, CancellationToken cancellationToken)
        => Task.FromResult(default(TResponse));

    /// <summary>The logger.</summary>
    protected ILogger Logger { get; private set; } = null!;

    /// <summary>The service provider.</summary>
    protected IServiceProvider Services { get; private set; } = null!;

    /// <summary>The App schema context.</summary>
    protected IAppSchemaContext AppContext { get; private set; } = null!;
}

/// <summary>Base request for App APIs.</summary>
public abstract class AppApiRequest
{
    [JsonIgnore]
    public HttpContext? HttpContext { get; set; }

    /// <summary>The locale hint.</summary>
    public string? Locale { get; set; }
}

/// <summary>Base response for App APIs.</summary>
public abstract class AppApiResponse
{
    /// <summary>Execution time in milliseconds.</summary>
    public long? ExecuteTime { get; set; }
}
