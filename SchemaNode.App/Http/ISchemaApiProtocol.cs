using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.OpenApi;
using SchemaNode.App.Components;
using SchemaNode.App.Enum;
using SchemaNode.App.Utility;
using SchemaNode.Context;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SchemaNode.App.Http;

/// <summary>
/// The protocol for schema APIs — controls how requests are read, validated and how responses are shaped.
/// </summary>
public interface ISchemaApiProtocol
{
    /// <summary>Gets the wrapped response schema for OpenAPI documentation.</summary>
    IOpenApiSchema WrapResponseSchema(DocumentFilterContext context, IOpenApiSchema innerSchema);

    /// <summary>Gets the wrapped request schema for OpenAPI documentation.</summary>
    IOpenApiSchema WrapRequestSchema(DocumentFilterContext context, IOpenApiSchema innerSchema);

    /// <summary>Read and deserialise the request body.</summary>
    TRequest ReadRequest<TRequest>(SchemaContext context, string requestBody) where TRequest : SchemaApiRequest;

    /// <summary>Generate a successful HTTP result from the response object.</summary>
    IResult GenerateResult<TResponse>(SchemaContext context, TResponse response) where TResponse : SchemaApiResponse;

    /// <summary>Generate an error HTTP result.</summary>
    IResult GenerateErrorResponse(SchemaContext context, SchemaApiErrorCode code, string? message = null,
        IReadOnlyDictionary<string, object>? data = null);

    /// <summary>
    /// Full request/response pipeline — parses, validates, dispatches, then shapes the result.
    /// </summary>
    public async Task<IResult> ProcessAsync<TApi, TRequest, TResponse>(HttpContext ctx)
        where TApi : SchemaApi<TRequest, TResponse>
        where TRequest : SchemaApiRequest
        where TResponse : SchemaApiResponse
    {
        var provider = ctx.RequestServices;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(TApi));
        var context = provider.GetRequiredService<SchemaContext>();

        logger.LogDebug("{name} API is being executed ...", typeof(TApi).Name);
        ctx.Request.EnableBuffering();

        string requestBody = "";
        IFormFileCollection? files = null;
        try
        {
            if (ctx.Request.ContentType != null &&
                ctx.Request.ContentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Request.Body.Position = 0;
                requestBody = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
            }
            else if (ctx.Request.ContentType != null &&
                     ctx.Request.ContentType.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            {
                files = ctx.Request.Form.Files;
                JsonObject result = new();
                foreach (var item in ctx.Request.Form)
                {
                    if (item.Value.Count == 0) continue;
                    string? data = item.Value[0];
                    if (string.IsNullOrWhiteSpace(data)) continue;

                    JsonNode? node;
                    try { node = JsonNode.Parse(data); }
                    catch { node = JsonValue.Create(data); }

                    if (node != null) result.Add(item.Key, node);
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
            context.SetRequestInfo(request.Locale, request.TimeZone, request.DateFormat);

            if (request.DateFormat != null && request.DateFormat != DateFormatMode.Iso8601)
                request = ReadRequest<TRequest>(context, requestBody);
        }
        catch (Exception ex)
        {
            return GenerateErrorResponse(context, SchemaApiErrorCode.ParseFailed, ex.Message);
        }

        try
        {
            List<ValidationResult> results = new();
            if (!Validator.TryValidateObject(request, new ValidationContext(request), results, true))
            {
                return GenerateErrorResponse(context, SchemaApiErrorCode.InvalidParams,
                    "The request parameters are invalid.", GetValidationErrors(results));
            }
        }
        catch (Exception ex)
        {
            return GenerateErrorResponse(context, SchemaApiErrorCode.InternalError, ex.GetInnermostException().Message);
        }

        Stopwatch watch = Stopwatch.StartNew();

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

        response!.ExecuteTime = watch.ElapsedMilliseconds;
        response!.TimeZone = context.GetTimeZone()?.Id;
        logger.LogDebug("{name} API executed in {time}ms.", typeof(TApi).Name, watch.ElapsedMilliseconds);

        // File stream download
        if (response.Output?.Stream != null)
        {
            string extension = Path.GetExtension(response.Output.Name);
            if (string.IsNullOrWhiteSpace(extension)) extension = response.Output.Extension;
            if (!string.IsNullOrWhiteSpace(extension) && !extension.StartsWith('.'))
                extension = $".{extension}";

            if (string.IsNullOrWhiteSpace(extension)
                || !new FileExtensionContentTypeProvider().TryGetContentType(extension, out string? contentType))
            {
                contentType = "application/octet-stream";
            }

            ctx.Response.Headers.AccessControlExposeHeaders = new StringValues("Content-Disposition");
            return Results.File(response.Output.Stream, contentType, fileDownloadName: response.Output.Name);
        }

        return GenerateResult(context, response);
    }

    /// <summary>Collect validation field errors.</summary>
    static Dictionary<string, object> GetValidationErrors(IEnumerable<ValidationResult> results)
    {
        var errors = new Dictionary<string, object>();
        foreach (var r in results)
        {
            string key = r.MemberNames.First();
            if (errors.ContainsKey(key)) continue;
            errors.Add(key, r.ErrorMessage ?? "");
        }
        return errors;
    }

    /// <summary>
    /// Build protocol metadata for the management UI.
    /// </summary>
    internal SchemaApiProtocolMeta GetProtocolMeta(IServiceProvider provider)
    {
        string name = GetType().Name;
        if (name.EndsWith("SchemaApiProtocol", StringComparison.OrdinalIgnoreCase))
            name = name[..^"SchemaApiProtocol".Length];
        else if (name.EndsWith("ApiProtocol", StringComparison.OrdinalIgnoreCase))
            name = name[..^"ApiProtocol".Length];
        else if (name.EndsWith("Protocol", StringComparison.OrdinalIgnoreCase))
            name = name[..^"Protocol".Length];

        var schemaGenerator = provider.GetRequiredService<ISchemaGenerator>();
        var schemaRepository = new SchemaRepository();
        var ctx = new DocumentFilterContext(
            Array.Empty<Microsoft.AspNetCore.Mvc.ApiExplorer.ApiDescription>(),
            schemaGenerator, schemaRepository);

        OpenApiSchema innerSchema = new();
        IOpenApiSchema reqSchema = WrapRequestSchema(ctx, innerSchema);
        IOpenApiSchema resSchema = WrapResponseSchema(ctx, innerSchema);

        var reqMeta = new SchemaApiProtocolRequestMeta();
        if (reqSchema is { Type: JsonSchemaType.Object, Properties: not null })
        {
            foreach (var (key, value) in reqSchema.Properties)
            {
                if (value == innerSchema) reqMeta.Wrap = key!;
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
                if (value == innerSchema) resMeta.Unwrap = key!;
                else
                {
                    resMeta.Fields ??= new Dictionary<string, JsonNode>();
                    resMeta.Fields[key] = GenerateJsonDesc(value);
                }
            }
        }

        return new SchemaApiProtocolMeta
        {
            Name = name,
            Request = reqMeta,
            Response = resMeta,
            SchemaFormat = ISchemaFormatProvider.GetSupportedFormats().ToArray(),
        };
    }

    JsonNode GenerateJsonDesc(IOpenApiSchema schema)
    {
        if (schema is { Type: JsonSchemaType.Object, Properties: not null })
        {
            JsonObject obj = new();
            foreach (var (key, value) in schema.Properties)
                obj[key!] = GenerateJsonDesc(value);
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
            return JsonValue.Create(
                $"{schema.Type.ToString()?.ToLower()}{(!string.IsNullOrEmpty(schema.Format) ? $"[{schema.Format}]" : "")}{(example != null ? $":{example}" : "")}");
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
