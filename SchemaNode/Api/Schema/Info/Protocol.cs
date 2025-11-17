using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchemaNode.Http;
using SchemaNode.Utility;

namespace SchemaNode.Api.Schema.Info;

/// <summary>
/// The Protocol api
/// </summary>
[NoProtocol]
public class ProtocolApi : SchemaApi<ProtocolRequest, ProtocolResponse>
{
    /// <inheritdoc />
    protected override async Task<ProtocolResponse?> ExecuteAsync(ProtocolRequest request,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]Protocol [Request]{request}", request);

        await Task.Yield();

        ISchemaApiProtocol apiProtocol = SchemaContext.ServiceProvider.GetRequiredService<ISchemaApiProtocol>();
        var protocolMeta = apiProtocol.GetProtocolMeta(SchemaContext.ServiceProvider);
        
        return new ProtocolResponse
        {
            Name = protocolMeta.Name,
            Request = protocolMeta.Request?.ToJsonNode(),
            Response = protocolMeta.Response?.ToJsonNode()
        };
    }
}

/// <summary>
/// The Protocol request
/// </summary>
public class ProtocolRequest : SchemaApiRequest
{
}

/// <summary>
/// The Protocol response
/// </summary>
public class ProtocolResponse : SchemaApiResponse
{
    public string? Name { get; init; }
    public JsonNode? Request { get; init; } 
    public JsonNode? Response { get; init; }
}