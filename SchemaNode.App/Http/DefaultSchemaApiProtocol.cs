using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi;
using SchemaNode.Context;
using SchemaNode.Utility;
using Swashbuckle.AspNetCore.SwaggerGen;

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
    public TRequest ReadRequest<TRequest>(SchemaContext context, string requestBody) where TRequest : SchemaApiRequest
    {
        return context.FromJson<TRequest>(requestBody) ?? throw new InvalidOperationException("Failed to parse request body.");
    }

    /// <inheritdoc />
    public IResult GenerateResult<TResponse>(SchemaContext context, TResponse response) where TResponse : SchemaApiResponse
    {
        return context.ToJsonResult(response);
    }
    
    /// <inheritdoc />
    public IResult GenerateErrorResponse(SchemaContext context, SchemaApiErrorCode code, string? message = null, IReadOnlyDictionary<string, object>? data = null)
    {
        return Results.InternalServerError(message);
    }
}