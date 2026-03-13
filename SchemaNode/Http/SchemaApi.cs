using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchemaNode.Context;
using SchemaNode.Components;
using SchemaNode.Enum;

namespace SchemaNode.Http;

public abstract class SchemaApi<TRequest, TResponse>
    where TRequest : SchemaApiRequest
    where TResponse : SchemaApiResponse
{
    #region Execute

    /// <summary>
    /// Execute the request, don't override or use it
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
    /// Process the request
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected virtual Task<TResponse?> ExecuteAsync(TRequest request, CancellationToken cancellationToken) => Task.FromResult(default(TResponse));
    
    #endregion
    
    #region Metadata
    
    /// <summary>
    /// The logger.
    /// </summary>
    protected ILogger Logger { get; private set; } = null!;

    /// <summary>
    /// The service provider.
    /// </summary>
    protected IServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// The schema context
    /// </summary>
    protected SchemaContext SchemaContext { get; private set; } = null!;
    
    #endregion
    
    #region Lock

    /// <summary>
    /// Lock by key
    /// </summary>
    protected Task<ICriticalRegion> GetLockAsync(string lockKey, params object[] args)
        => _criticalRegionProvider.Value.AcquireAsync(string.Format(lockKey, args));

    /// <summary>
    /// Lock by key with timeout
    /// </summary>
    protected Task<ICriticalRegion> GetLockAsync(string lockKey, TimeSpan timeout, params object[] args)
        => _criticalRegionProvider.Value.AcquireAsync(string.Format(lockKey, args), timeout);

    /// <summary>
    /// The critical region provider
    /// </summary>
    private Lazy<ICriticalRegionProvider> _criticalRegionProvider = null!;

    #endregion
}

#region Inner Types

/// <summary>
/// Contains the base implementation api request.
/// </summary>
public abstract class SchemaApiRequest
{
    /// <summary>
    /// The http context
    /// </summary>
    [JsonIgnore]
    public HttpContext? Context { get; set; }
    
    /// <summary>
    /// The upload files
    /// </summary>
    [JsonIgnore]
    public IFormFileCollection? Files { get; set; }
    
    /// <summary>
    /// The locale setting
    /// </summary>
    public string? Locale { get; set; }
    
    /// <summary>
    /// The date format mode
    /// </summary>
    public DateFormatMode? DateFormat { get; set; }

    /// <summary>
    /// The time zone, e.g. "Pacific Standard Time", "UTC", "Asia/Shanghai"
    /// </summary>
    public string? TimeZone { get; set; }
}

/// <summary>
/// Contains the base implementation api response.
/// </summary>
public abstract class SchemaApiResponse
{
    /// <summary>
    /// The stream to be downloading
    /// </summary>
    [JsonIgnore]
    public SchemaApiFile? Output { get; set; }
    
    /// <summary>
    /// The execution time in milliseconds
    /// </summary>
    public long? ExecuteTime { get; set; }

    /// <summary>
    /// The time zone to handle the request
    /// </summary>
    public string? TimeZone { get; set; }
}

/// <summary>
/// The file as the response
/// </summary>
public class SchemaApiFile
{
    /// <summary>
    /// The output file name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The output file extension if file name not provide
    /// </summary>
    public string Extension { get; set; } = string.Empty;

    /// <summary>
    /// The output stream
    /// </summary>
    public required Stream Stream { get; set; }
}

#endregion