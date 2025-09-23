using SchemaNode.Http;

namespace SchemaNode.Example.Api;

/// <summary>
/// The Hello api
/// </summary>
public class HelloApi : SchemaApi<HelloRequest, HelloResponse>
{
    #region Constructors

    /// <inheritdoc />
    public HelloApi(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    #endregion

    #region Main

    /// <inheritdoc />
    protected override async Task<HelloResponse> MainAsync(HelloRequest request)
    {
        Logger.LogDebug("[Api]{api} [Request]{request}", nameof(HelloApi), request);

        await Task.Yield();

        // Return the response
        return new HelloResponse()
        {
            Response = $"Hi, {request.Name}"
        };
    }

    #endregion
}