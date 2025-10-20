using SchemaNode.Http;

namespace SchemaNode.Example.Api;

/// <summary>
/// The Hello api
/// </summary>
public class HelloApi : SchemaApi<HelloRequest, HelloResponse>
{
    /// <inheritdoc />
    protected override async Task<HelloResponse?> ExecuteAsync(HelloRequest request, CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]{api} [Request]{request}", nameof(HelloApi), request);

        await Task.Yield();

        // Return the response
        return new HelloResponse()
        {
            Response = $"Hi, {request.Name}"
        };
    }
}

/// <summary>
/// The Hello request data
/// </summary>
public class HelloRequest : SchemaApiRequest
{
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// The Hello response data
/// </summary>
public class HelloResponse: SchemaApiResponse
{
    public required string Response { get; set; } 
}