namespace SchemaNode.Example;

/// <summary>
/// Represents a raw response message in JSON-RPC of a microservice API.
/// </summary>
public class MicroserviceApiResponseMessage<TResponse>
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
    public MicroserviceApiResponseError? Error { get; set; }

    /// <summary>
    /// It MUST be the same as the value of the id member in the Request Object. If there was an error in detecting the id in the Request object (e.g. Parse error/Invalid Request), it MUST be Null.
    /// </summary>
    public string? Id { get; set; }
}

/// <summary>
/// Contains the error information of a microservice API response.
/// </summary>
public sealed class MicroserviceApiResponseError
{
    /// <summary>
    /// Indicates the error type that occurred.
    /// </summary>
    public MicroserviceApiResponseErrorCode Code { get; set; }

    /// <summary>
    /// Provides a key of the error description for programatic use.
    /// </summary>
    public string? MessageKey { get; set; }

    /// <summary>
    /// Provides a short description of the error for human read.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// A string hash structure that contains additional information about the error.
    /// </summary>
    public IDictionary<string, object>? Data { get; set; }
}

/// <summary>
/// Defines the reserved error codes, from and including -32768 to -32000.
/// </summary>
public enum MicroserviceApiResponseErrorCode
{
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