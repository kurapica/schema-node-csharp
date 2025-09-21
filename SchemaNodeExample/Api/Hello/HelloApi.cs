using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace SchemaNode.Example.Api;

/// <summary>
/// The Hello api
/// </summary>
[MicroserviceApiCategory("bd.user.test", nameof(HelloApi))]
public class HelloApi : MicroserviceApi<HelloRequest, HelloResponse>
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
        Components.Logger.LogDebug("[Api]{api} [Request]{request}", nameof(HelloApi), request);

        await Task.Yield();

        // Return the response
        return new HelloResponse()
        {
            Response = $"Hi, {request.Name}"
        };
    }

    #endregion
}