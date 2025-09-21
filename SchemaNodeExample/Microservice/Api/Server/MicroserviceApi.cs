using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.HttpSys;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using SchemaNode.Utility;
using System;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Security.AccessControl;
using System.Text.Json;

namespace SchemaNode.Example;

/// <summary>
/// The microsoft api
/// Contains the base implementation of a microservice API endpoint, which follows JSON-RPC 2.0 communication protocol.
/// </summary>
public abstract class MicroserviceApi<TRequest, TResponse> : Controller
    where TRequest : MicroserviceApiRequest
{
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    protected MicroserviceApi(IServiceProvider serviceProvider)
    {
        Components = new MicroserviceComponents(this, serviceProvider);
    }

    #endregion

    #region Metadata

    /// <summary>
    /// Gets the category of the API.
    /// </summary>
    protected virtual string Category => string.Empty;

    /// <summary>
    /// Gets the components.
    /// </summary>
    protected MicroserviceComponents Components { get; }

    #endregion

    #region Main

    /// <summary>
    /// Execute the request
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ExecuteAsync()
    {
        // Parse request.
        Components.Logger.LogDebug("API is being executed ...");
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
            Components.Logger.LogDebug(errorMessage);
            MicroserviceApiResponseMessage<TResponse> errorResponseMessage = GenerateErrorResponseMessage(MicroserviceApiResponseErrorCode.ParseError, errorMessage);
            return GenerateResponseResult(errorResponseMessage);
        }
        MicroserviceApiRequestMessage<TRequest> requestMessage;
        try
        {
            requestMessage = requestBody.FromJson<MicroserviceApiRequestMessage<TRequest>>() ?? throw new Exception();
            RequestId = requestMessage.Id;
        }
        catch
        {
            string errorMessage = "Failed to parse the request data.";
            Components.Logger.LogDebug(errorMessage);
            MicroserviceApiResponseMessage<TResponse> errorResponseMessage = GenerateErrorResponseMessage(MicroserviceApiResponseErrorCode.InvalidRequest, errorMessage);
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
            Components.Logger.LogDebug(ex.Message);
            MicroserviceApiResponseMessage<TResponse> errorResponseMessage = GenerateErrorResponseMessage(MicroserviceApiResponseErrorCode.InvalidRequest, ex.Message);
            return GenerateResponseResult(errorResponseMessage);
        }
        TRequest request = requestMessage.Params;

        // Validate request.
        try
        {
            List<ValidationResult> results = new();
            if (!Validator.TryValidateObject(request, new ValidationContext(request), results, true))
            {
                MicroserviceApiResponseMessage<TResponse> errorResponseMessage = GenerateErrorResponseMessage(MicroserviceApiResponseErrorCode.InvalidParams, "The request parameters are invalid.", data: GetValidationErrors(results));
                return GenerateResponseResult(errorResponseMessage);
            }
        }
        catch (Exception ex)
        {
            Components.Logger.LogDebug($"An unknown error occurred: {ex.GetInnermostException()}");
            MicroserviceApiResponseMessage<TResponse> errorResponseMessage = GenerateErrorResponseMessage(MicroserviceApiResponseErrorCode.InternalError, ex.GetInnermostException().Message);
            return GenerateResponseResult(errorResponseMessage);
        }

        // Call main.
        TResponse response;
        try
        {
            request.CancellationToken = Request.HttpContext.RequestAborted;
            response = await MainAsync(request);
        }
        catch (MicroserviceApiException ex)
        {
            Components.Logger.LogDebug($"A business logic error occurred: {ex.Message}");
            MicroserviceApiResponseMessage<TResponse> errorResponseMessage = GenerateErrorResponseMessage(ex.Code, ex.Message, ex.MessageKey, ex.AdditionalData);
            return GenerateResponseResult(errorResponseMessage);
        }
        catch (Exception ex)
        {
            Components.Logger.LogDebug($"An unknown error occurred: {ex.GetInnermostException()}");
            MicroserviceApiResponseMessage<TResponse> errorResponseMessage = GenerateErrorResponseMessage(MicroserviceApiResponseErrorCode.InternalError, ex.GetInnermostException().Message);
            return GenerateResponseResult(errorResponseMessage);
        }

        // Generate response.
        Components.Logger.LogDebug("API is executed.");
        MicroserviceApiResponseMessage<TResponse> responseMessage = GenerateSuccessResponseMessage(response);
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
    protected MicroserviceApiException CreateParameterException(IDictionary<string, string> errorMessages)
    {
        IDictionary<string, object> errorData = errorMessages.ToDictionary(keyValuePair => keyValuePair.Key, keyValuePair => (object)keyValuePair.Value);
        return new MicroserviceApiException(MicroserviceApiResponseErrorCode.InvalidParams, "The request parameters are invalid.", data: errorData);
    }

    /// <summary>
    /// Creates an exception that represents a parameter error.
    /// </summary>
    [DebuggerHidden]
    protected MicroserviceApiException CreateParameterException(string field, string message)
    {
        return new MicroserviceApiException(MicroserviceApiResponseErrorCode.InvalidParams, "The request parameters are invalid.", data: new Dictionary<string, object>
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
            if (r is CompositeValidationResult rc)
            {
                errors.Add(key, GetValidationErrors(rc.Results));
            }
            else
            {
                errors.Add(key, r.ErrorMessage ?? "");
            }
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

    MicroserviceApiResponseMessage<TResponse> GenerateSuccessResponseMessage(TResponse response)
    {
        return new MicroserviceApiResponseMessage<TResponse>
        {
            Jsonrpc = "2.0",
            Result = response,
            Id = RequestId
        };
    }

    MicroserviceApiResponseMessage<TResponse> GenerateErrorResponseMessage(MicroserviceApiResponseErrorCode code, string? message = null, string? messageKey = null, IDictionary<string, object>? data = null)
    {
        return new MicroserviceApiResponseMessage<TResponse>
        {
            Jsonrpc = "2.0",
            Error = new MicroserviceApiResponseError
            {
                Code = code,
                Message = message,
                MessageKey = messageKey,
                Data = data
            },
            Id = RequestId
        };
    }

    IActionResult GenerateResponseResult(MicroserviceApiResponseMessage<TResponse> responseMessage)
    {
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
        => Components.CriticalRegionProvider.AcquireAsync(string.Format(lockKey, args));

    /// <summary>
    /// Lock by key with timeout
    /// </summary>
    protected Task<ICriticalRegion> GetLockAsync(string lockKey, TimeSpan timeout, params object[] args)
        => Components.CriticalRegionProvider.AcquireAsync(string.Format(lockKey, args), timeout);

    /// <summary>
    /// Lock by entity class
    /// </summary>
    protected async Task<ICriticalRegion> GetLockAsync<T>(params object[] args)
        => await GetLockAsync(ENTITY_CLASS_LOCK_KEY, typeof(T).FullName!, args is { Length: > 0 } ? string.Join(':', args) : string.Empty);

    /// <summary>
    /// Lock by entity class with timeout 
    /// </summary>
    protected async Task<ICriticalRegion> GetLockAsync<T>(TimeSpan timeout, params object[] args)
        => await GetLockAsync(ENTITY_CLASS_LOCK_KEY, timeout, typeof(T).FullName!, args is { Length: > 0 } ? string.Join(':', args) : string.Empty);

    const string ENTITY_CLASS_LOCK_KEY = "ENTITY_LOCK:{0}:{1}";

    #endregion
}


/// <summary>
/// The MicroserviceApi DI
/// </summary>
public static class MicroserviceApiExtension
{
    /// <summary>
    /// Ad MicroserviceApis
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static IMvcBuilder AddMicroserviceApis<T>(this IMvcBuilder builder)
    {
        return AddMicroserviceApis(builder, typeof(T).Assembly);
    }

    public static IMvcBuilder AddMicroserviceApis(this IMvcBuilder builder, Assembly aseembly)
    {
        if (builder.PartManager.ApplicationParts.All(p => p.Name != aseembly.GetName().Name))
            builder.AddApplicationPart(aseembly);

        registerAseemblys.Push(aseembly);
        return builder;
    }

    /// <summary>
    /// Enable microservice apis
    /// </summary>
    /// <param name="app"></param>
    /// <param name="swagger"></param>
    /// <param name="prefix"></param>
    /// <param name="suffix"></param>
    /// <returns></returns>
    public static WebApplication UseMicroserviceApis(this WebApplication app, bool swagger = true, string prefix = "", string suffix = "")
    {
        UrlPrefix = prefix;
        UrlSuffix = suffix;

        while (registerAseemblys.TryPop(out Assembly? assembly))
        {
            if (assembly == null) continue;
            foreach (Type type in assembly.GetTypes().Where(t => t.IsSubclassOfGenericType(typeof(MicroserviceApi<,>)) && !t.IsAbstract))
            {
                Type apiBaseType = type.GetGenericBaseType(typeof(MicroserviceApi<,>))!;
                if (apiBaseType == null) continue;
                Type requestType = apiBaseType.GetGenericArguments()[0];
                Type responseType = apiBaseType.GetGenericArguments()[1];
                apiTypes.Push(new MicroserviceApiType(type, requestType, responseType));
                app.MapControllerRoute(type.Name, GetRequestUrl(requestType), new
                {
                    controller = type.Name,
                    action = "Execute"
                });
                Console.WriteLine($"<{type.Name}> is now listening.");
            }
        }

        if (swagger)
        {
            Assembly assembly = typeof(Program).Assembly;
            string baseNamespace = "SchemaNode.Example.Microservice.Api.Swagger";
            EmbeddedFileProvider fileProvider = new(assembly, baseNamespace);
            app.UseDefaultFiles(new DefaultFilesOptions
            {
                FileProvider = fileProvider,
                DefaultFileNames = new List<string>
                {
                    "index.html"
                }
            });
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = fileProvider
            });
            app.MapControllerRoute(nameof(MicroserviceApiDocument), MicroserviceApiDocument.URL, new
            {
                controller = nameof(MicroserviceApiDocument),
                action = nameof(MicroserviceApiDocument.Execute)
            });

        }
        return app;
    }


    #region Utility

    /// <summary>
    /// Gets all apis
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<MicroserviceApiType> GetApis()
    {
        foreach(var api in apiTypes)
        {
            yield return api;
        }
    }

    /// <summary>
    /// Gets the request url
    /// </summary>
    public static string GetRequestUrl<T>() where T : MicroserviceApiRequest
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

    static ConcurrentStack<Assembly> registerAseemblys = new();
    static ConcurrentStack<MicroserviceApiType> apiTypes = new();
    public record MicroserviceApiType(Type Api, Type Request, Type Response);

    #endregion
}