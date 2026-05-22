using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchemaNode.App.Components;
using SchemaNode.Context;
using SchemaNode.App.Enum;

namespace SchemaNode.App.Http;

/// <summary>
/// Base class for all SchemaNode App API handlers.
/// </summary>
public abstract class SchemaApi<TRequest, TResponse>
    where TRequest : SchemaApiRequest
    where TResponse : SchemaApiResponse
{
    #region Execute

    /// <summary>
    /// Execute the request — called by the dispatcher. Do not override or call directly.
    /// </summary>
    public async Task<TResponse?> _ExecuteAsync(TRequest request, ILogger logger)
    {
        Services = request.Context!.RequestServices;
        Logger = logger;
        _criticalRegionProvider = new Lazy<ICriticalRegionProvider>(Services.GetRequiredService<ICriticalRegionProvider>);
        SchemaContext = Services.GetRequiredService<SchemaContext>();
        return await ExecuteAsync(request, request.Context.RequestAborted);
    }

    /// <summary>
    /// Override to implement the API logic.
    /// </summary>
    protected virtual Task<TResponse?> ExecuteAsync(TRequest request, CancellationToken cancellationToken)
        => Task.FromResult(default(TResponse));

    #endregion

    #region Metadata

    /// <summary>The logger.</summary>
    protected ILogger Logger { get; private set; } = null!;

    /// <summary>The service provider.</summary>
    protected IServiceProvider Services { get; private set; } = null!;

    /// <summary>The schema context.</summary>
    protected SchemaContext SchemaContext { get; private set; } = null!;

    #endregion

    #region Lock

    /// <summary>Lock by key.</summary>
    protected Task<ICriticalRegion> GetLockAsync(string lockKey, params object[] args)
        => _criticalRegionProvider.Value.AcquireAsync(string.Format(lockKey, args));

    /// <summary>Lock by key with timeout.</summary>
    protected Task<ICriticalRegion> GetLockAsync(string lockKey, TimeSpan timeout, params object[] args)
        => _criticalRegionProvider.Value.AcquireAsync(string.Format(lockKey, args), timeout);

    private Lazy<ICriticalRegionProvider> _criticalRegionProvider = null!;

    #endregion
}

/// <summary>
/// Base request for all SchemaApi handlers.
/// </summary>
public abstract class SchemaApiRequest
{
    /// <summary>The HTTP context — injected by the dispatcher.</summary>
    [JsonIgnore]
    public HttpContext? Context { get; set; }

    /// <summary>Uploaded files (for multipart/form-data requests).</summary>
    [JsonIgnore]
    public IFormFileCollection? Files { get; set; }

    /// <summary>Locale hint (e.g. "en-US").</summary>
    public string? Locale { get; set; }

    /// <summary>Date format mode.</summary>
    public DateFormatMode? DateFormat { get; set; }

    /// <summary>Time zone identifier (e.g. "Asia/Shanghai", "UTC").</summary>
    public string? TimeZone { get; set; }
}

/// <summary>
/// Base response for all SchemaApi handlers.
/// </summary>
public abstract class SchemaApiResponse
{
    /// <summary>The download file stream, if the response is a file download.</summary>
    [JsonIgnore]
    public SchemaApiFile? Output { get; set; }

    /// <summary>Execution time in milliseconds.</summary>
    public long? ExecuteTime { get; set; }

    /// <summary>The time zone used to handle the request.</summary>
    public string? TimeZone { get; set; }
}

/// <summary>
/// Represents a file to be streamed as the API response.
/// </summary>
public class SchemaApiFile
{
    /// <summary>The download file name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>File extension (used when Name has no extension).</summary>
    public string Extension { get; set; } = string.Empty;

    /// <summary>The stream to send.</summary>
    public required Stream Stream { get; set; }
}
