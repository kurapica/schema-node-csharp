using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using SchemaNode.Utility;
using Swashbuckle.AspNetCore.SwaggerGen;
using static SchemaNode.Utility.Extension;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Http.JsonRpc;

/// <summary>
/// The JSON RPC schema API processor
/// </summary>
public class JsonRpcSchemaApiProcessor: ISchemaApiProcessor
{
    #region Implements ISchemaApiProcessor
    
    /// <inheritdoc />
    public OpenApiSchema WrapRequestSchema(DocumentFilterContext context, OpenApiSchema innerSchema)
    {
        return new OpenApiSchema
        {
            Type = "object",
            Properties = new Dictionary<string, OpenApiSchema>
            {
                ["jsonrpc"] = new OpenApiSchema { Type = "string", Example = new OpenApiString("2.0") },
                ["id"] = new OpenApiSchema { Type = "string", Example = new OpenApiString(Guid.NewGuid().ToString()) },
                ["method"] = new OpenApiSchema { Type = "string" },
                ["params"] = innerSchema
            },
            Required = new HashSet<string> { "jsonrpc", "id", "params" }
        };
    }

    /// <inheritdoc />
    public OpenApiSchema WrapResponseSchema(DocumentFilterContext context, OpenApiSchema innerSchema)
    {
        return new OpenApiSchema
        {
            Type = "object",
            Properties = new Dictionary<string, OpenApiSchema>
            {
                ["jsonrpc"] = new OpenApiSchema { Type = "string", Example = new OpenApiString("2.0") },
                ["id"] = new OpenApiSchema { Type = "string" },
                ["code"] = new OpenApiSchema { Type = "integer", Example = new OpenApiInteger(0) },
                ["error"] = context.SchemaGenerator.GenerateSchema(typeof(JsonRpcResponseError), context.SchemaRepository),
                ["result"] = innerSchema,
            },
        };
    }

    /// <inheritdoc />
    public TRequest ReadRequest<TRequest>(string requestBody) where TRequest : SchemaApiRequest
    {
        JsonRpcRequestMessage<TRequest> requestMessage = requestBody.FromJson<JsonRpcRequestMessage<TRequest>>() 
                ?? throw new Exception("Failed to parse the request body.");
        if (requestMessage.Jsonrpc != "2.0" || requestMessage.Params == null || string.IsNullOrEmpty(requestMessage.Id))
            throw new ArgumentException("The request message does not follow JSON-RPC protocol strictly.");
        _requestId = requestMessage.Id;
        return requestMessage.Params;
    }

    /// <inheritdoc />
    public IResult GenerateResult<TResponse>(TResponse response) where TResponse : SchemaApiResponse
    {
        return Results.Json(new JsonRpcResponseMessage<TResponse>
        {
            Jsonrpc = "2.0",
            Result = response,
            Id = _requestId,
        }, NoIndentJsonOption);
    }

    public IResult GenerateErrorResponse(SchemaApiErrorCode code, string? message = null,
        IReadOnlyDictionary<string, object>? data = null)
    {
        return Results.Json( new JsonRpcResponseMessage<SchemaApiResponse>
        {
            Jsonrpc = "2.0",
            Error = new JsonRpcResponseError
            {
                Code = code switch
                {
                    SchemaApiErrorCode.None => 0,
                    SchemaApiErrorCode.ParseFailed => (int)JsonRpcResponseErrorCode.ParseError,
                    SchemaApiErrorCode.InvalidParams => (int)JsonRpcResponseErrorCode.InvalidParams,
                    _ => (int)JsonRpcResponseErrorCode.InternalError
                },
                Message = message,
                Data = data,
            },
            Id = _requestId,
        }, NoIndentJsonOption);
    }

    #endregion

    #region Utility

    string? _requestId;
    
    #endregion
}

/// <summary>
/// Represents a raw request message in JSON-RPC of a microservice API.
/// </summary>
public sealed class JsonRpcRequestMessage<TRequest>
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
public class JsonRpcResponseMessage<TResponse>
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
    public JsonRpcResponseError? Error { get; set; }

    /// <summary>
    /// It MUST be the same as the value of the id member in the Request Object. If there was an error in detecting the id in the Request object (e.g. Parse error/Invalid Request), it MUST be Null.
    /// </summary>
    public string? Id { get; set; }
}

/// <summary>
/// Contains the error information of a microservice API response.
/// </summary>
public sealed class JsonRpcResponseError
{
    /// <summary>
    /// Indicates the error type that occurred.
    /// </summary>
    public int Code { get; set; }

    /// <summary>
    /// Provides a short description of the error for human read.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// A string hash structure that contains additional information about the error.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Data { get; set; }
}

/// <summary>
/// Defines the reserved error codes, from and including -32768 to -32000.
/// </summary>
public enum JsonRpcResponseErrorCode
{
    Ok = 0,
    
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