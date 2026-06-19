using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.OpenApi;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Function;
using SchemaNode.Utility;
using Swashbuckle.AspNetCore.SwaggerGen;
using TimeZoneConverter;

// ReSharper disable CollectionNeverQueried.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Http;

/// <summary>
/// The protocol for schema APIs
/// </summary>
public interface ISchemaApiProtocol
{
    /// <summary>
    /// Gets the wrapped response schema.
    /// </summary>
    IOpenApiSchema WrapResponseSchema(DocumentFilterContext context, IOpenApiSchema innerSchema);

    /// <summary>
    /// Gets the wrapped request schema.
    /// </summary>
    IOpenApiSchema WrapRequestSchema(DocumentFilterContext context, IOpenApiSchema innerSchema);

    /// <summary>
    /// Read request from body
    /// </summary>
    TRequest ReadRequest<TRequest>(SchemaContext context, string requestBody) where TRequest : SchemaApiRequest;

    /// <summary>
    /// Generate the result based on the response
    /// </summary>
    IResult GenerateResult<TResponse>(SchemaContext context, TResponse response) where TResponse : SchemaApiResponse;

    /// <summary>
    /// Generate error response based on exception
    /// </summary>
    IResult GenerateErrorResponse(SchemaContext context, SchemaApiErrorCode code, string? message = null,
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
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger<TApi>();
        var context = provider.GetRequiredService<SchemaContext>();

        // Parse request.
        logger.LogDebug("{name} API is being executed ...", typeof(TApi).Name);
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
                    if (item.Value.Count ==0) continue;
                    string? data = item.Value[0];
                    if (string.IsNullOrWhiteSpace(data)) continue;

                    JsonNode? node;
                    try
                    {
                        node = JsonNode.Parse(data);
                    }
                    catch
                    {
                        node = JsonValue.Create(data);
                    }
                    
                    if (node != null)
                        result.Add(item.Key, node);
                }
                requestBody = result.ToString();
            }
        }
        catch
        {
            return GenerateErrorResponse(context, SchemaApiErrorCode.ParseFailed, "Failed to read the request data.");
        }
        
        
        TRequest request;
        try
        {
            request = ReadRequest<TRequest>(context, requestBody);
            
            // time zone
            if (!string.IsNullOrWhiteSpace(request.TimeZone) && TZConvert.TryGetTimeZoneInfo(request.TimeZone, out var tz))
                context.SetContextItem(tz);
            
            // date format
            if (request.DateFormat.HasValue && request.DateFormat != DateFormatMode.Iso8601)
            {
                context.SetContextItem(request.DateFormat.Value);
                request = ReadRequest<TRequest>(context, requestBody); // re-read the request with the correct date format
            }
        }
        catch (Exception ex)
        {
            return GenerateErrorResponse(context, SchemaApiErrorCode.ParseFailed,  ex.Message);
        }

        // Validate request.
        try
        {
            List<ValidationResult> results = new();
            if (!Validator.TryValidateObject(request, new ValidationContext(request), results, true))
            {
                return GenerateErrorResponse(context, SchemaApiErrorCode.InvalidParams, "The request parameters are invalid.", GetValidationErrors(results));
            }
        }
        catch (Exception ex)
        {
            return GenerateErrorResponse(context, SchemaApiErrorCode.InternalError, ex.GetInnermostException().Message);
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
        catch (UnauthorizedAccessException ex)
        {
            logger.LogError(ex, "{name} API execution failed - Unauthorized.", typeof(TApi).Name);
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{name} API execution failed.", typeof(TApi).Name);
            return GenerateErrorResponse(context, SchemaApiErrorCode.InternalError, ex.GetInnermostException().Message);
        }
        finally
        {
            watch.Stop();
        }

        // Generate response.
        response!.ExecuteTime = watch.ElapsedMilliseconds;
        response.TimeZone = context.GetTimeZone().Id;
        logger.LogDebug("{name} API is executed, cost {time}.", typeof(TApi).Name, watch.ElapsedMilliseconds);
        
        // Stream
        if (response.Output?.Stream != null)
        {
            string extension = Path.GetExtension(response.Output.Name);
            if (string.IsNullOrWhiteSpace(extension)) extension = response.Output.Extension;
            if (!string.IsNullOrWhiteSpace(extension) && !extension.StartsWith('.'))
                extension = $".{extension}";

            if (string.IsNullOrWhiteSpace(extension)
                || !new FileExtensionContentTypeProvider()
                    .TryGetContentType(extension, out string? contentType))
            {
                contentType = "application/octet-stream";
            }

            ctx.Response.Headers.AccessControlExposeHeaders = new StringValues("Content-Disposition");

            return Results.File(
                response.Output.Stream,
                contentType,
                fileDownloadName: response.Output.Name
            );
        }
        return GenerateResult(context, response);
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
    
    /// <summary>
    /// Generate the protocol meta based on openapi schema
    /// </summary>
    internal SchemaApiProtocolMeta GetProtocolMeta(IServiceProvider provider)
    {
        string name = GetType().Name;
        if (name.EndsWith("SchemaApiProtocol", StringComparison.OrdinalIgnoreCase))
            name = name.Substring(0, name.Length - "SchemaApiProtocol".Length);
        else if (name.EndsWith("ApiProtocol", StringComparison.OrdinalIgnoreCase))
            name = name.Substring(0, name.Length - "ApiProtocol".Length);
        else if (name.EndsWith("Protocol", StringComparison.OrdinalIgnoreCase))
            name = name.Substring(0, name.Length - "Protocol".Length);
        
        var schemaGenerator = provider.GetRequiredService<ISchemaGenerator>();
        var schemaRepository = new SchemaRepository();

        // No need to generate the real schema here, we just want to analyze the structure of the wrapped schema,
        // so we can use a placeholder schema as the inner schema and check if it's referenced in the properties of the wrapped schema.
        var context = new DocumentFilterContext(Array.Empty<Microsoft.AspNetCore.Mvc.ApiExplorer.ApiDescription>(),
            schemaGenerator, schemaRepository);
        OpenApiSchema innerSchema = new(); // placeholder
        
        IOpenApiSchema reqSchema = WrapRequestSchema(context, innerSchema);
        IOpenApiSchema resSchema = WrapResponseSchema(context, innerSchema);
        
        var reqMeta = new SchemaApiProtocolRequestMeta();
        if (reqSchema is { Type: JsonSchemaType.Object, Properties: not null })
        {
            foreach (var (key, value) in reqSchema.Properties)
            {
                if (value == innerSchema)
                {
                    reqMeta.Wrap = key;
                }
                else
                {
                    reqMeta.Fields ??= new Dictionary<string, JsonNode>();
                    reqMeta.Fields[key] = GenerateJsonDesc(value);
                }
            }
        }
        
        var resMeta = new SchemaApiProtocolResponseMeta();
        if (resSchema is { Type: JsonSchemaType.Object, Properties: not null })
        {
            foreach (var (key, value) in resSchema.Properties)
            {
                if (value == innerSchema)
                {
                    resMeta.Unwrap = key;
                }
                else
                {
                    resMeta.Fields ??= new Dictionary<string, JsonNode>();
                    resMeta.Fields[key] = GenerateJsonDesc(value);
                }
            }
        }
        return new SchemaApiProtocolMeta{
            Name= name,
            Request= reqMeta,
            Response= resMeta,
            SchemaFormat = ISchemaFormatProvider.GetSupportedFormats().ToArray(),
        };
    }

    JsonNode GenerateJsonDesc(IOpenApiSchema schema)
    {
        if (schema is { Type: JsonSchemaType.Object, Properties: not null })
        {
            JsonObject obj = new();
            foreach (var (key, value) in schema.Properties)
            {
                obj[key] = GenerateJsonDesc(value);
            }
            return obj;
        }
        else if (schema is { Type: JsonSchemaType.Array, Items: not null })
        {
            JsonArray arr = new();
            arr.Add(GenerateJsonDesc(schema.Items));
            return arr;
        }
        else
        {
            string? example = schema.Example?.ToString();
            return JsonValue.Create($"{schema.Type.ToString()?.ToLower()}{(!string.IsNullOrEmpty(schema.Format) ? $"[{schema.Format}]" : "")}{(example != null ? $":{example}" : "")}");
        }
    }
}

internal class SchemaApiProtocolRequestMeta
{
    public string? Wrap { get; set; }
    public Dictionary<string, JsonNode>? Fields { get; set; }
}

internal class SchemaApiProtocolResponseMeta
{
    public string? Unwrap { get; set; }
    public Dictionary<string, JsonNode>? Fields { get; set; }
}

internal class SchemaApiProtocolMeta
{
    public string? Name { get; init; }
    public SchemaApiProtocolRequestMeta? Request { get; init; } 
    public SchemaApiProtocolResponseMeta? Response { get; init; }
    public string[]? SchemaFormat { get; init; }
}