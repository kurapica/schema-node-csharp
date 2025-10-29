using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using SchemaNode.Utility;
using Swashbuckle.AspNetCore.SwaggerGen;
using static SchemaNode.Utility.Extension;

namespace SchemaNode.Http;

/// <summary>
/// The default schema api processor
/// </summary>
public class DefaultSchemaApiProcessor: ISchemaApiProcessor
{
    /// <inheritdoc />
    public OpenApiSchema WrapResponseSchema(DocumentFilterContext context, OpenApiSchema innerSchema) => innerSchema;

    /// <inheritdoc />
    public OpenApiSchema WrapRequestSchema(DocumentFilterContext context, OpenApiSchema innerSchema) => innerSchema;
    
    /// <inheritdoc />
    public TRequest ReadRequest<TRequest>(string requestBody) where TRequest : SchemaApiRequest
    {
        return requestBody.FromJson<TRequest>() ?? throw new Exception();
    }

    /// <inheritdoc />
    public IResult GenerateResult<TResponse>(TResponse response) where TResponse : SchemaApiResponse
    {
        return Results.Json(response, NoIndentJsonOption);
    }
    
    /// <inheritdoc />
    public IResult GenerateErrorResponse(SchemaApiErrorCode code, string? message = null, IReadOnlyDictionary<string, object>? data = null)
    {
        return Results.InternalServerError(message);
    }
}