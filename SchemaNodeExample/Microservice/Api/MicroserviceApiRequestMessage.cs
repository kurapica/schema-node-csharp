namespace SchemaNode.Example;

/// <summary>
/// Represents a raw request message in JSON-RPC of a microservice API.
/// </summary>
public sealed class MicroserviceApiRequestMessage<TRequest>
    where TRequest : MicroserviceApiRequest
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