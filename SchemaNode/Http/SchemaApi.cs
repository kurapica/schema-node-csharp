using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using SchemaNode.Context;
using SchemaNode.Components;
using SchemaNode.Components.Provider;
using SchemaNode.Utility;

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
        _schemaContext = new Lazy<SchemaContext>(Services.GetRequiredService<SchemaContext>);
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
    protected SchemaContext SchemaContext => _schemaContext.Value;
    private Lazy<SchemaContext> _schemaContext = null!;
    
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

/// <summary>
/// Represents a raw request message in JSON-RPC of a microservice API.
/// </summary>
public sealed class SchemaApiRequestMessage<TRequest>
    where TRequest : SchemaApiRequest
{
    /// <summary>
    /// The version of the JSON-RPC protocol. MUST be exactly "2.0".
    /// </summary>
    public string Jsonrpc { get; set; } = "2.0";

    /// <summary>
    /// The name of the method to be invoked.
    /// </summary>
    public string? Method { get; set; }

    /// <summary>
    /// A Structured value that holds the parameter values to be used during the invocation of the method. This member MAY be omitted.
    /// </summary>
    public TRequest? Params { get; set; }

    /// <summary>
    /// An identifier established by the Client.
    /// </summary>
    public required string Id { get; set; }
}

/// <summary>
/// Represents a raw response message in JSON-RPC of a microservice API.
/// </summary>
public class SchemaApiResponseMessage<TResponse>
    where TResponse : SchemaApiResponse
{
    /// <summary>
    /// The version of the JSON-RPC protocol. MUST be exactly "2.0".
    /// </summary>
    public string Jsonrpc { get; set; } = "2.0";

    /// <summary>
    /// The actual result which is REQUIRED on success. This member MUST NOT exist if there was an error invoking the method.
    /// </summary>
    public TResponse? Result { get; set; }

    /// <summary>
    /// The error information. This member MUST NOT exist if there was no error triggered during invocation.
    /// </summary>
    public SchemaApiResponseError? Error { get; set; }

    /// <summary>
    /// It MUST be the same as the value of the id member in the Request Object. If there was an error in detecting the id in the Request object (e.g. Parse error/Invalid Request), it MUST be Null.
    /// </summary>
    public string? Id { get; set; }
    
    /// <summary>
    /// The api execute time(in ms)
    /// </summary>
    public long? ExecuteTime { get; set; }
}

/// <summary>
/// Contains the error information of a microservice API response.
/// </summary>
public sealed class SchemaApiResponseError
{
    /// <summary>
    /// Indicates the error type that occurred.
    /// </summary>
    public SchemaApiResponseErrorCode Code { get; set; }

    /// <summary>
    /// Provides a key of the error description for programmatic use.
    /// </summary>
    public string? MessageKey { get; set; }

    /// <summary>
    /// Provides a short description of the error for human read.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// A string hash structure that contains additional information about the error.
    /// </summary>
    public IDictionary<string, object>? Data { get; set; }
}

/// <summary>
/// Defines the reserved error codes, from and including -32768 to -32000.
/// </summary>
public enum SchemaApiResponseErrorCode
{
    /// <summary>
    /// The pre-defined error code lower bound.
    /// </summary>
    Min = -32768,

    /// <summary>
    /// Invalid JSON was received by the server.
    /// </summary>
    ParseError = -32700,

    /// <summary>
    /// The JSON sent is not a valid Request object.
    /// </summary>
    InvalidRequest = -32600,

    /// <summary>
    /// The method does not exist / is not available.
    /// </summary>
    MethodNotFound = -32601,

    /// <summary>
    /// Invalid method parameter(s).
    /// </summary>
    InvalidParams = -32602,

    /// <summary>
    /// Internal JSON-RPC error.
    /// </summary>
    InternalError = -32603,

    /// <summary>
    /// The API has returned a business logic error.
    /// </summary>
    BusinessError = -32099,

    /// <summary>
    /// The pre-defined error code upper bound.
    /// </summary>
    Max = -32000,

    /// <summary>
    /// Authentication failed
    /// </summary>
    AuthFailed = 100,
}

/// <summary>
/// Thrown when the request argument is invalid of a microservice API.
/// </summary>
public class SchemaApiException : Exception
{
    #region Constructors

    /// <summary>
    /// The microservice api exception
    /// </summary>
    public SchemaApiException(SchemaApiResponseErrorCode code, string message, string? messageKey = null, IDictionary<string, object>? data = null, Exception? innerException = null) : base(message, innerException)
    {
        Code = code;
        MessageKey = messageKey;
        AdditionalData = data;
    }

    #endregion

    #region Error Messages

    /// <summary>
    /// Gets the code.
    /// </summary>
    public SchemaApiResponseErrorCode Code { get; }

    /// <summary>
    /// Gets the key, if any.
    /// </summary>
    public string? MessageKey { get; }

    /// <summary>
    /// Gets the additional data.
    /// </summary>
    public IDictionary<string, object>? AdditionalData { get; }

    #endregion
}

#endregion

#region Extension

/// <summary>
/// The SchemaApi DI
/// </summary>
public static class SchemaApiExtension
{
    /// <summary>
    /// Add schema apis in an Assembly of the given type
    /// </summary>
    public static WebApplication AddSchemaApis<T>(this WebApplication app)
    {
        return AddSchemaApis(app, typeof(T).Assembly);
    }

    /// <summary>
    /// Add schema apis in an assembly, the entry assembly and SchemaNode will be added automatically
    /// </summary>
    public static WebApplication AddSchemaApis(this WebApplication app, Assembly? assembly = null)
    {
        assembly ??= Assembly.GetEntryAssembly();
        if (RegisterAssemblys.Contains(assembly)) return app;
        RegisterAssemblys.Add(assembly!);
        return app;
    }

    /// <summary>
    /// Enable microservice apis
    /// </summary>
    public static IEndpointRouteBuilder UseSchemaApis(this IEndpointRouteBuilder app, string prefix = "schema", string suffix = "", bool enableAppDataApi = false)
    {
        UrlPrefix = prefix;
        UrlSuffix = suffix;
        
        // add default Assembly
        Assembly schemaAssembly = typeof(SchemaApiExtension).Assembly;
        if (!RegisterAssemblys.Contains(schemaAssembly)) RegisterAssemblys.Add(schemaAssembly);
        Assembly? assembly = Assembly.GetEntryAssembly();
        if (!RegisterAssemblys.Contains(assembly)) RegisterAssemblys.Add(assembly!);

        IServiceProviderIsService service = app.ServiceProvider.GetRequiredService<IServiceProviderIsService>();
        bool hasSchemaStorage = service.IsService(typeof(ISchemaStorageProvider));
        bool hasAppDataStorage = service.IsService(typeof(IAppSchemaDataProvider));

        while (RegisterAssemblys.TryTake(out assembly))
        {
            foreach (Type type in assembly.GetTypes().Where(t => t.IsSubclassOfGenericType(typeof(SchemaApi<,>)) && !t.IsAbstract))
            {
                // Register schema apis based on services
                if (assembly == schemaAssembly)
                {
                    // no storage no edit
                    if (type.FullName!.Contains("api.schema.edit", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!hasSchemaStorage) continue;
                    }
                    else if (type.FullName!.Contains("api.schema.application", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!hasAppDataStorage || !enableAppDataApi) continue;
                    }
                }
                
                Type apiBaseType = type.GetGenericBaseType(typeof(SchemaApi<,>))!;
                Type requestType = apiBaseType.GetGenericArguments()[0];
                Type responseType = apiBaseType.GetGenericArguments()[1];

                ApiTypes.Push(new SchemaApiType(type, requestType, responseType));

                MethodInfo task = typeof(SchemaApiExtension).GetMethod(nameof(ProcessSchemaApiAsync),BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(type, requestType, responseType);
                app.MapPost(GetRequestUrl(requestType), async (HttpContext ctx) =>
                {
                    Task<IResult> res = (Task<IResult>)task.Invoke(null, [ctx])!;
                    return await res;
                });
                Console.WriteLine($"<{type.Name}> is now listening.");
            }
        }
        return app;
    }


    #region Utility
    
    static readonly AsyncLocal<Stopwatch> StopWatch = new ();

    private static JsonSerializerOptions JsonOptions = new ()
    {
        WriteIndented = false,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
            new Extension.JsonDateTimeIsoConverter(),
            new Extension.JsonDateTimeOffsetIsoConverter(),
            new Extension.ForceStringConverter(),
        },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
    
    static async Task<IResult> ProcessSchemaApiAsync<TApi, TRequest, TResponse>(HttpContext ctx) 
        where TApi: SchemaApi<TRequest, TResponse>
        where TRequest: SchemaApiRequest
        where TResponse: SchemaApiResponse
    {
        StopWatch.Value ??= new Stopwatch();
        StopWatch.Value.Start();
        
        var provider = ctx.RequestServices;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(TApi));

        // Parse request.
        logger.LogDebug("{0} API is being executed ...", typeof(TApi).Name);
        ctx.Request.EnableBuffering();
        string requestBody = "";
        string requestId = string.Empty;
        IFormFileCollection? files = null;
        try
        {
            if (ctx.Request.ContentType != null && ctx.Request.ContentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Request.Body.Position = 0;
                requestBody = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
            }
            else if (ctx.Request.ContentType != null && ctx.Request.ContentType.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            {
                files = ctx.Request.Form.Files;
                JsonObject result = new ();
                foreach (KeyValuePair<string, StringValues> item in ctx.Request.Form)
                {
                    if (!string.IsNullOrEmpty(item.Value))
                        result.Add(item.Key, item.Key.Equals("Params", StringComparison.OrdinalIgnoreCase) ? JsonNode.Parse(item.Value!) : JsonValue.Create(item.Value));
                }
                requestBody = result.ToString();
            }
        }
        catch
        {
            return Results.Json(GenErrorResponseMessage(requestId, SchemaApiResponseErrorCode.ParseError, "Failed to read the request data."), JsonOptions);
        }
        SchemaApiRequestMessage<TRequest> requestMessage;
        try
        {
            requestMessage = requestBody.FromJson<SchemaApiRequestMessage<TRequest>>() ?? throw new Exception();
            requestId = requestMessage.Id;
        }
        catch(Exception ex)
        {
            return Results.Json(GenErrorResponseMessage(requestId, SchemaApiResponseErrorCode.InvalidRequest, $"Failed to parse the request data - {ex.GetInnermostException().Message}"), JsonOptions);
        }
        try
        {
            if (requestMessage.Jsonrpc != "2.0" || requestMessage.Params == null || string.IsNullOrEmpty(requestMessage.Id))
            {
                throw new ArgumentException("The request message does not follow JSON-RPC protocol strictly.");
            }
        }
        catch (Exception ex)
        {
            return Results.Json(GenErrorResponseMessage(requestId, SchemaApiResponseErrorCode.InvalidRequest, ex.Message), JsonOptions);
        }
        TRequest request = requestMessage.Params;

        // Validate request.
        try
        {
            List<ValidationResult> results = new();
            if (!Validator.TryValidateObject(request, new ValidationContext(request), results, true))
            {
                return Results.Json(GenErrorResponseMessage(requestId, SchemaApiResponseErrorCode.InvalidParams, "The request parameters are invalid.", data: GetValidationErrors(results)), JsonOptions);
            }
        }
        catch (Exception ex)
        {
            return Results.Json(GenErrorResponseMessage(requestId, SchemaApiResponseErrorCode.InternalError, ex.GetInnermostException().Message), JsonOptions);
        }

        // Call main.
        TResponse? response;
        try
        {
            TApi api = ActivatorUtilities.CreateInstance<TApi>(provider);
            request.Context = ctx;
            request.Files = files;
            response = await api._ExecuteAsync(request, logger);
        }
        catch (SchemaApiException ex)
        {
            logger.LogDebug($"A business logic error occurred: {ex.Message}");
            return Results.Json(GenErrorResponseMessage(requestId, ex.Code, ex.Message, data: ex.AdditionalData), JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogDebug($"An unknown error occurred: {ex.GetInnermostException()}");
            return Results.Json(GenErrorResponseMessage(requestId, SchemaApiResponseErrorCode.InternalError, ex.GetInnermostException().Message), JsonOptions);
        }

        // Generate response.
        StopWatch.Value?.Stop();
        logger.LogDebug("{0} API is executed, cost {1}.", typeof(TApi).Name, StopWatch.Value?.ElapsedMilliseconds);
        
        // Stream
        if (response?.Output?.Stream != null)
        {
            string extension = Path.GetExtension(response.Output.Name);
            if (string.IsNullOrWhiteSpace(extension)) extension = response.Output.Extension;
            if (!string.IsNullOrWhiteSpace(extension) && !extension.StartsWith('.')) extension = $".{extension}";
            if (string.IsNullOrWhiteSpace(extension) || !new FileExtensionContentTypeProvider().TryGetContentType(extension, out string? contentType))
                contentType = "text/plain";
            FileStreamResult result = new(response.Output.Stream, contentType);
            if (!string.IsNullOrWhiteSpace(response.Output.Name))
            {
                result.FileDownloadName = response.Output.Name;
            }
            ctx.Response.Headers.AccessControlExposeHeaders = new StringValues("Content-Disposition");
            return Results.File(response.Output.Stream);
        }

        return Results.Json(new SchemaApiResponseMessage<TResponse>
        {
            Jsonrpc = "2.0",
            Result = response,
            Id = requestId,
            ExecuteTime = StopWatch.Value?.ElapsedMilliseconds
        }, JsonOptions);
    }

    /// <summary>
    /// Get validation errors
    /// </summary>
    static Dictionary<string, object> GetValidationErrors(IEnumerable<ValidationResult> results)
    {
        Dictionary<string, object> errors = new();
        foreach (ValidationResult r in results)
        {
            string key = r.MemberNames.First();
            if (errors.ContainsKey(key))
                continue;
            errors.Add(key, r.ErrorMessage ?? "");
        }
        return errors;
    }

    static SchemaApiResponseMessage<SchemaApiResponse> GenErrorResponseMessage(string id, SchemaApiResponseErrorCode code, string? message = null, string? messageKey = null, IDictionary<string, object>? data = null)
    {
        StopWatch.Value?.Stop();
        return new SchemaApiResponseMessage<SchemaApiResponse>
        {
            Jsonrpc = "2.0",
            Error = new SchemaApiResponseError
            {
                Code = code,
                Message = message,
                MessageKey = messageKey,
                Data = data,
            },
            Id = id,
            ExecuteTime = StopWatch.Value?.ElapsedMilliseconds
        };
    }

    /// <summary>
    /// Gets all apis
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<(SchemaApiType, string url)> GetSchemaApis()
    {
        foreach(var api in ApiTypes)
        {
            yield return (api, GetRequestUrl(api.Request));
        }
    }

    public static string GetRequestUrl(Type type)
    {
        string name = type.Name.EndsWith("request", StringComparison.OrdinalIgnoreCase)
            ? type.Name[..^"Request".Length]
            : type.Name;
        List<string> nameSegments = new();
        int nameSegmentStartIndex = 0;
        for (int i = 1; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]))
            {
                nameSegments.Add(name[nameSegmentStartIndex..i].ToLowerInvariant());
                nameSegmentStartIndex = i;
            }
        }
        // Add tail
        nameSegments.Add(name[nameSegmentStartIndex..].ToLowerInvariant());
        if (!nameSegments.Any())
            nameSegments.Add(name.ToLowerInvariant());
        return $"{(string.IsNullOrWhiteSpace(UrlPrefix) ? "" : $"{UrlPrefix}/")}{nameSegments.Aggregate(string.Empty, (segment0, segment) => !string.IsNullOrEmpty(segment0) ? $"{segment0}-{segment}" : segment)}{(string.IsNullOrWhiteSpace(UrlSuffix) ? "" : $".{UrlSuffix}")}";
    }

    /// <summary>
    /// Url prefix
    /// </summary>
    static string UrlPrefix { get; set; } = "";

    /// <summary>
    /// Url suffix
    /// </summary>
    static string UrlSuffix { get; set; } = "";

    static readonly ConcurrentBag<Assembly> RegisterAssemblys = new();
    static readonly ConcurrentStack<SchemaApiType> ApiTypes = new();
    public record SchemaApiType(Type Api, Type Request, Type Response);

    #endregion
}

#endregion