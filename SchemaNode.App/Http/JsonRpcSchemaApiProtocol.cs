using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi;
using SchemaNode.App.Utility;
using SchemaNode.Context;
using Swashbuckle.AspNetCore.SwaggerGen;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.App.Http.JsonRpc;

/// <summary>
/// JSON-RPC 2.0 protocol adapter for <see cref="ISchemaApiProtocol"/>.
/// </summary>
public class JsonRpcSchemaApiProtocol : ISchemaApiProtocol
{
    #region ISchemaApiProtocol

    /// <inheritdoc />
    public IOpenApiSchema WrapRequestSchema(DocumentFilterContext context, IOpenApiSchema innerSchema)
        => new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["jsonrpc"] = new OpenApiSchema { Type = JsonSchemaType.String, Example = "2.0" },
                ["id"]      = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid", Example = Guid.NewGuid().ToString() },
                ["method"]  = new OpenApiSchema { Type = JsonSchemaType.String, Format = "url" },
                ["params"]  = innerSchema
            },
            Required = new HashSet<string> { "jsonrpc", "id", "params" }
        };

    /// <inheritdoc />
    public IOpenApiSchema WrapResponseSchema(DocumentFilterContext context, IOpenApiSchema innerSchema)
        => new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["jsonrpc"] = new OpenApiSchema { Type = JsonSchemaType.String, Example = "2.0" },
                ["id"]      = new OpenApiSchema { Type = JsonSchemaType.String },
                ["code"]    = new OpenApiSchema { Type = JsonSchemaType.Integer, Example = 0 },
                ["error"]   = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Properties = new Dictionary<string, IOpenApiSchema>
                    {
                        ["code"]    = new OpenApiSchema { Type = JsonSchemaType.Integer },
                        ["message"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "error" },
                        ["data"]    = new OpenApiSchema { Type = JsonSchemaType.Object },
                    }
                },
                ["result"] = innerSchema,
            },
        };

    /// <inheritdoc />
    public TRequest ReadRequest<TRequest>(SchemaContext context, string requestBody) where TRequest : SchemaApiRequest
    {
        JsonRpcRequestMessage<TRequest> msg = context.FromJson<JsonRpcRequestMessage<TRequest>>(requestBody)
            ?? throw new Exception("Failed to parse the request body.");
        if (msg.Jsonrpc != "2.0" || msg.Params == null || string.IsNullOrEmpty(msg.Id))
            throw new ArgumentException("The request message does not follow JSON-RPC protocol strictly.");
        _requestId = msg.Id;
        return msg.Params;
    }

    /// <inheritdoc />
    public IResult GenerateResult<TResponse>(SchemaContext context, TResponse response) where TResponse : SchemaApiResponse
        => context.ToJsonResult(new JsonRpcResponseMessage<TResponse>
        {
            Jsonrpc = "2.0",
            Result  = response,
            Id      = _requestId,
        });

    /// <inheritdoc />
    public IResult GenerateErrorResponse(SchemaContext context, SchemaApiErrorCode code, string? message = null,
        IReadOnlyDictionary<string, object>? data = null)
        => context.ToJsonResult(new JsonRpcResponseMessage<SchemaApiResponse>
        {
            Jsonrpc = "2.0",
            Error = new JsonRpcResponseError
            {
                Code = code switch
                {
                    SchemaApiErrorCode.None         => 0,
                    SchemaApiErrorCode.ParseFailed   => (int)JsonRpcResponseErrorCode.ParseError,
                    SchemaApiErrorCode.InvalidParams => (int)JsonRpcResponseErrorCode.InvalidParams,
                    _                               => (int)JsonRpcResponseErrorCode.InternalError
                },
                Message = message,
                Data    = data,
            },
            Id = _requestId,
        });

    #endregion

    string? _requestId;
}

/// <summary>Request envelope for JSON-RPC 2.0.</summary>
public sealed class JsonRpcRequestMessage<TRequest> where TRequest : SchemaApiRequest
{
    public string Jsonrpc { get; set; } = "2.0";
    public string? Method { get; set; }
    public TRequest? Params { get; set; }
    public required string Id { get; set; }
}

/// <summary>Response envelope for JSON-RPC 2.0.</summary>
public class JsonRpcResponseMessage<TResponse> where TResponse : SchemaApiResponse
{
    public string Jsonrpc { get; set; } = "2.0";
    public TResponse? Result { get; set; }
    public JsonRpcResponseError? Error { get; set; }
    public string? Id { get; set; }
}

/// <summary>Error payload for a JSON-RPC 2.0 response.</summary>
public sealed class JsonRpcResponseError
{
    public int Code { get; set; }
    public string? Message { get; set; }
    public IReadOnlyDictionary<string, object>? Data { get; set; }
}

/// <summary>Standard JSON-RPC 2.0 error codes.</summary>
public enum JsonRpcResponseErrorCode
{
    Ok           = 0,
    Min          = -32768,
    ParseError   = -32700,
    InvalidRequest = -32600,
    MethodNotFound = -32601,
    InvalidParams = -32602,
    InternalError = -32603,
    BusinessError = -32099,
    Max          = -32000,
    AuthFailed   = 100,
}
