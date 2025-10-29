using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.OpenApi.Models;
using SchemaNode.Utility;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SchemaNode.Http;

/// <summary>
/// The processor for schema APIs
/// </summary>
public interface ISchemaApiProcessor
{
    /// <summary>
    /// Gets the wrapped response schema.
    /// </summary>
    OpenApiSchema WrapResponseSchema(DocumentFilterContext context, OpenApiSchema innerSchema);

    /// <summary>
    /// Gets the wrapped request schema.
    /// </summary>
    OpenApiSchema WrapRequestSchema(DocumentFilterContext context, OpenApiSchema innerSchema);

    /// <summary>
    /// Read request from body
    /// </summary>
    TRequest ReadRequest<TRequest>(string requestBody) where TRequest : SchemaApiRequest;

    IResult GenerateResult<TResponse>(TResponse response) where TResponse : SchemaApiResponse;

    /// <summary>
    /// Generate error response based on exception
    /// </summary>
    IResult GenerateErrorResponse(SchemaApiErrorCode code, string? message = null,
        IReadOnlyDictionary<string, object>? data = null);
    
    /// <summary>
    /// Process the schema api request
    /// </summary>
    public async Task<IResult> ProcessAsync<TApi, TRequest, TResponse>(HttpContext ctx)
        where TApi : SchemaApi<TRequest, TResponse>
        where TRequest : SchemaApiRequest
        where TResponse : SchemaApiResponse
    {
        var provider = ctx.RequestServices;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(TApi));

        // Parse request.
        logger.LogDebug("{0} API is being executed ...", typeof(TApi).Name);
        ctx.Request.EnableBuffering();
        
        string requestBody = "";
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
            return GenerateErrorResponse(SchemaApiErrorCode.ParseFailed, "Failed to read the request data.");
        }
        
        
        TRequest request;
        try
        {
            request = ReadRequest<TRequest>(requestBody);
        }
        catch (Exception ex)
        {
            return GenerateErrorResponse(SchemaApiErrorCode.ParseFailed,  ex.Message);
        }

        // Validate request.
        try
        {
            List<ValidationResult> results = new();
            if (!Validator.TryValidateObject(request, new ValidationContext(request), results, true))
            {
                return GenerateErrorResponse(SchemaApiErrorCode.InvalidParams, "The request parameters are invalid.", GetValidationErrors(results));
            }
        }
        catch (Exception ex)
        {
            return GenerateErrorResponse(SchemaApiErrorCode.InternalError, ex.GetInnermostException().Message);
        }

        Stopwatch watch = Stopwatch.StartNew();

        // Call main.
        TResponse? response;
        try
        {
            TApi api = provider.GetRequiredService<TApi>();
            request.Context = ctx;
            request.Files = files;
            response = await api._ExecuteAsync(request, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{0} API execution failed.", typeof(TApi).Name);
            return GenerateErrorResponse(SchemaApiErrorCode.InternalError, ex.GetInnermostException().Message);
        }
        finally
        {
            watch.Stop();
        }

        // Generate response.
        response!.ExecuteTime = watch.ElapsedMilliseconds;
        logger.LogDebug("{0} API is executed, cost {1}.", typeof(TApi).Name, watch.ElapsedMilliseconds);
        
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
        
        return GenerateResult(response!);
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
            if (errors.ContainsKey(key)) continue;
            errors.Add(key, r.ErrorMessage ?? "");
        }
        return errors;
    }
}
