using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using SchemaNode.DI;
using SchemaNode.Utility;

namespace SchemaNode.Http;

public abstract class SchemaApi<TRequest, TResponse>
    where TRequest : SchemaApiRequest
    where TResponse : SchemaApiResponse
{
    
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    protected SchemaApi(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
        Logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(this.GetType());
        _criticalRegionProviderThunk = new Lazy<ICriticalRegionProvider>(serviceProvider.GetRequiredService<ICriticalRegionProvider>);
    }

    #endregion

    #region Metadata

    /// <summary>
    /// The service provider.
    /// </summary>
    protected IServiceProvider ServiceProvider { get; }
    
    /// <summary>
    /// The logger.
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Gets the <see cref="ICriticalRegion" /> provider.
    /// </summary>
    protected ICriticalRegionProvider CriticalRegionProvider => _criticalRegionProviderThunk.Value;
    readonly Lazy<ICriticalRegionProvider> _criticalRegionProviderThunk;

    #endregion

    #region Main
    
    /// <summary>
    /// Process the request
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public virtual Task<TResponse?> ProcessAsync(TRequest request, CancellationToken cancellationToken) => Task.FromResult(default(TResponse));

    /// <summary>
    /// Execute the request
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ExecuteAsync()
    {
        // Parse request.
        Logger.LogDebug("API is being executed ...");
        Request.EnableBuffering();
        string requestBody;
        try
        {
            Request.Body.Position = 0;
            requestBody = await new StreamReader(Request.Body).ReadToEndAsync();
        }
        catch
        {
            string errorMessage = "Failed to read the request data.";
            Logger.LogDebug(errorMessage);
            SchemaApiResponseMessage<TResponse> errorResponseMessage = GenerateErrorResponseMessage(SchemaApiResponseErrorCode.ParseError, errorMessage);
            return GenerateResponseResult(errorResponseMessage);
        }
        SchemaApiRequestMessage<TRequest> requestMessage;
        try
        {
            requestMessage = requestBody.FromJson<SchemaApiRequestMessage<TRequest>>() ?? throw new Exception();
            RequestId = requestMessage.Id;
        }
        catch
        {
            string errorMessage = "Failed to parse the request data.";
            Logger.LogDebug(errorMessage);
            SchemaApiResponseMessage<TResponse> errorResponseMessage = GenerateErrorResponseMessage(SchemaApiResponseErrorCode.InvalidRequest, errorMessage);
            return GenerateResponseResult(errorResponseMessage);
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
            Logger.LogDebug(ex.Message);
            SchemaApiResponseMessage<TResponse> errorResponseMessage = GenerateErrorResponseMessage(SchemaApiResponseErrorCode.InvalidRequest, ex.Message);
            return GenerateResponseResult(errorResponseMessage);
        }
        TRequest request = requestMessage.Params;

        // Validate request.
        try
        {
            List<ValidationResult> results = new();
            if (!Validator.TryValidateObject(request, new ValidationContext(request), results, true))
            {
                SchemaApiResponseMessage<TResponse> errorResponseMessage = GenerateErrorResponseMessage(SchemaApiResponseErrorCode.InvalidParams, "The request parameters are invalid.", data: GetValidationErrors(results));
                return GenerateResponseResult(errorResponseMessage);
            }
        }
        catch (Exception ex)
        {
            Logger.LogDebug($"An unknown error occurred: {ex.GetInnermostException()}");
            SchemaApiResponseMessage<TResponse> errorResponseMessage = GenerateErrorResponseMessage(SchemaApiResponseErrorCode.InternalError, ex.GetInnermostException().Message);
            return GenerateResponseResult(errorResponseMessage);
        }

        // Call main.
        TResponse response;
        try
        {
            request.CancellationToken = Request.HttpContext.RequestAborted;
            response = await MainAsync(request);
        }
        catch (SchemaApiException ex)
        {
            Logger.LogDebug($"A business logic error occurred: {ex.Message}");
            SchemaApiResponseMessage<TResponse> errorResponseMessage = GenerateErrorResponseMessage(ex.Code, ex.Message, ex.MessageKey, ex.AdditionalData);
            return GenerateResponseResult(errorResponseMessage);
        }
        catch (Exception ex)
        {
            Logger.LogDebug($"An unknown error occurred: {ex.GetInnermostException()}");
            SchemaApiResponseMessage<TResponse> errorResponseMessage = GenerateErrorResponseMessage(SchemaApiResponseErrorCode.InternalError, ex.GetInnermostException().Message);
            return GenerateResponseResult(errorResponseMessage);
        }

        // Generate response.
        Logger.LogDebug("API is executed.");
        SchemaApiResponseMessage<TResponse> responseMessage = GenerateSuccessResponseMessage(response);
        return GenerateResponseResult(responseMessage);
    }

    /// <summary>
    /// Called to handle request and generate response.
    /// </summary>
    protected abstract Task<TResponse> MainAsync(TRequest request);

    /// <summary>
    /// Creates an exception that represents a parameter error.
    /// </summary>
    [DebuggerHidden]
    protected SchemaApiException CreateParameterException(IDictionary<string, string> errorMessages)
    {
        IDictionary<string, object> errorData = errorMessages.ToDictionary(keyValuePair => keyValuePair.Key, keyValuePair => (object)keyValuePair.Value);
        return new SchemaApiException(SchemaApiResponseErrorCode.InvalidParams, "The request parameters are invalid.", data: errorData);
    }

    /// <summary>
    /// Creates an exception that represents a parameter error.
    /// </summary>
    [DebuggerHidden]
    protected SchemaApiException CreateParameterException(string field, string message)
    {
        return new SchemaApiException(SchemaApiResponseErrorCode.InvalidParams, "The request parameters are invalid.", data: new Dictionary<string, object>
        {
            { field, message }
        });
    }

    /// <summary>
    /// Get validation errors
    /// </summary>
    protected Dictionary<string, object> GetValidationErrors(IEnumerable<ValidationResult> results)
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

    #endregion

    #region States

    /// <summary>
    /// Gets the request ID.
    /// </summary>
    protected string RequestId { get; private set; } = string.Empty;

    #endregion

    #region Implementations

    SchemaApiResponseMessage<TResponse> GenerateSuccessResponseMessage(TResponse response)
    {
        return new SchemaApiResponseMessage<TResponse>
        {
            Jsonrpc = "2.0",
            Result = response,
            Id = RequestId
        };
    }

    SchemaApiResponseMessage<TResponse> GenerateErrorResponseMessage(SchemaApiResponseErrorCode code, string? message = null, string? messageKey = null, IDictionary<string, object>? data = null)
    {
        return new SchemaApiResponseMessage<TResponse>
        {
            Jsonrpc = "2.0",
            Error = new SchemaApiResponseError
            {
                Code = code,
                Message = message,
                MessageKey = messageKey,
                Data = data
            },
            Id = RequestId
        };
    }

    IActionResult GenerateResponseResult(SchemaApiResponseMessage<TResponse> responseMessage)
    {
        // Stream
        if (responseMessage.Result?.Output?.Stream != null)
        {
            string extension = Path.GetExtension(responseMessage.Result.Output.Name);
            if (string.IsNullOrWhiteSpace(extension)) extension = responseMessage.Result.Output.Extension;
            if (!string.IsNullOrWhiteSpace(extension) && !extension.StartsWith('.')) extension = $".{extension}";
            if (string.IsNullOrWhiteSpace(extension) || !new FileExtensionContentTypeProvider().TryGetContentType(extension, out string? contentType))
                contentType = "text/plain";
            FileStreamResult result = new(responseMessage.Result.Output.Stream, contentType);
            if (!string.IsNullOrWhiteSpace(responseMessage.Result.Output.Name))
            {
                result.FileDownloadName = responseMessage.Result.Output.Name;
            }
            Response.Headers.AccessControlExposeHeaders = new StringValues("Content-Disposition");
            return result;
        }
        
        string responseBody = responseMessage.ToJson();
        return new ContentResult
        {
            Content = responseBody,
            ContentType = "application/json",
            StatusCode = (int)HttpStatusCode.OK
        };
    }

    #endregion

    #region Lock

    /// <summary>
    /// Lock by key
    /// </summary>
    protected Task<ICriticalRegion> GetLockAsync(string lockKey, params object[] args)
        => CriticalRegionProvider.AcquireAsync(string.Format(lockKey, args));

    /// <summary>
    /// Lock by key with timeout
    /// </summary>
    protected Task<ICriticalRegion> GetLockAsync(string lockKey, TimeSpan timeout, params object[] args)
        => CriticalRegionProvider.AcquireAsync(string.Format(lockKey, args), timeout);

    #endregion
}

#region Inner Types

/// <summary>
/// Contains the base implementation of a microservice API request.
/// </summary>
public abstract class SchemaApiRequest
{
    #region Cancel Token

    /// <summary>
    /// Cancel token
    /// </summary>
    [JsonIgnore]
    public CancellationToken CancellationToken { get; set; }

    #endregion
}

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
    /// Provides a key of the error description for programatic use.
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
    /// Add SchemaApis
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static IMvcBuilder AddSchemaApis<T>(this IMvcBuilder builder)
    {
        return AddSchemaApis(builder, typeof(T).Assembly);
    }

    public static IMvcBuilder AddSchemaApis(this IMvcBuilder builder, Assembly? aseembly = null)
    {
        aseembly ??= Assembly.GetEntryAssembly();
        if (RegisterAssemblys.Contains(aseembly)) return builder;
        RegisterAssemblys.Add(aseembly!);
        
        // register default schema apis
        AddSchemaApis(builder, typeof(SchemaApiExtension).Assembly);
        
        if (builder.PartManager.ApplicationParts.All(p => p.Name != aseembly!.GetName().Name))
            builder.AddApplicationPart(aseembly!);

        return builder;
    }

    /// <summary>
    /// Enable microservice apis
    /// </summary>
    /// <param name="app"></param>
    /// <param name="prefix"></param>
    /// <param name="suffix"></param>
    /// <returns></returns>
    public static WebApplication UseSchemaApis(this WebApplication app, string prefix = "", string suffix = "")
    {
        UrlPrefix = prefix;
        UrlSuffix = suffix;

        while (RegisterAssemblys.TryTake(out Assembly? assembly))
        {
            foreach (Type type in assembly.GetTypes().Where(t => t.IsSubclassOfGenericType(typeof(SchemaApi<,>)) && !t.IsAbstract))
            {
                Type apiBaseType = type.GetGenericBaseType(typeof(SchemaApi<,>))!;
                Type requestType = apiBaseType.GetGenericArguments()[0];
                Type responseType = apiBaseType.GetGenericArguments()[1];
                ApiTypes.Push(new SchemaApiType(type, requestType, responseType));
                app.MapControllerRoute(type.Name, GetRequestUrl(requestType), new
                {
                    controller = type.Name,
                    action = "Execute"
                });
                app.MapPost(GetRequestUrl(requestType),  async (HttpContext ctx) =>
                {
                    await Task.Yield();
                    ctx.Request.EnableBuffering();
                    return Results.Ok("hi");
                });
                Console.WriteLine($"<{type.Name}> is now listening.");
            }
        }
        return app;
    }


    #region Utility

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

    /// <summary>
    /// Gets the request url
    /// </summary>
    public static string GetRequestUrl<T>() where T : SchemaApiRequest
    {
        return GetRequestUrl(typeof(T));
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
    public static string UrlPrefix { get; set; } = "";

    /// <summary>
    /// Url suffix
    /// </summary>
    public static string UrlSuffix { get; set; } = "";

    static readonly ConcurrentBag<Assembly> RegisterAssemblys = new();
    static readonly ConcurrentStack<SchemaApiType> ApiTypes = new();
    public record SchemaApiType(Type Api, Type Request, Type Response);

    #endregion
}

#endregion