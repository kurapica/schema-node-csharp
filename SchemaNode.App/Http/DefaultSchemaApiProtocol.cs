using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi;
using SchemaNode.Context;
using SchemaNode.App.Utility;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SchemaNode.App.Http;

/// <summary>
/// The default schema API protocol — plain JSON in, plain JSON out.
/// </summary>
public class DefaultSchemaApiProtocol : ISchemaApiProtocol
{
    /// <inheritdoc />
    public IOpenApiSchema WrapResponseSchema(DocumentFilterContext context, IOpenApiSchema innerSchema) => innerSchema;

    /// <inheritdoc />
    public IOpenApiSchema WrapRequestSchema(DocumentFilterContext context, IOpenApiSchema innerSchema) => innerSchema;

    /// <inheritdoc />
    public TRequest ReadRequest<TRequest>(SchemaContext context, string requestBody) where TRequest : SchemaApiRequest
        => context.FromJson<TRequest>(requestBody) ?? throw new InvalidOperationException("Failed to parse request body.");

    /// <inheritdoc />
    public IResult GenerateResult<TResponse>(SchemaContext context, TResponse response) where TResponse : SchemaApiResponse
        => context.ToJsonResult(response);

    /// <inheritdoc />
    public IResult GenerateErrorResponse(SchemaContext context, SchemaApiErrorCode code, string? message = null,
        IReadOnlyDictionary<string, object>? data = null)
        => Results.InternalServerError(message);
}
