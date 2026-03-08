using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi;
using SchemaNode.Enum;
using SchemaNode.Utility;
using Swashbuckle.AspNetCore.SwaggerGen;
using static SchemaNode.Utility.Extension;

namespace SchemaNode.Http;

/// <summary>
/// The default schema api processor
/// </summary>
public class DefaultSchemaApiProtocol: ISchemaApiProtocol
{
    /// <inheritdoc />
    public IOpenApiSchema WrapResponseSchema(DocumentFilterContext context, IOpenApiSchema innerSchema) => innerSchema;

    /// <inheritdoc />
    public IOpenApiSchema WrapRequestSchema(DocumentFilterContext context, IOpenApiSchema innerSchema) => innerSchema;
    
    /// <inheritdoc />
    public TRequest ReadRequest<TRequest>(string requestBody, DateFormatMode? mode = null) where TRequest : SchemaApiRequest
    {
        return requestBody.FromJson<TRequest>(mode) ?? throw new Exception();
    }

    /// <inheritdoc />
    public IResult GenerateResult<TResponse>(TResponse response, DateFormatMode? mode = null, TimeZoneInfo? timeZone = null) where TResponse : SchemaApiResponse
    {
        return Results.Json(response, GetJsonOptions(false, mode, timeZone));
    }
    
    /// <inheritdoc />
    public IResult GenerateErrorResponse(SchemaApiErrorCode code, string? message = null, IReadOnlyDictionary<string, object>? data = null)
    {
        return Results.InternalServerError(message);
    }
}