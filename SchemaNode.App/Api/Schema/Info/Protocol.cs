using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using SchemaNode.Http;
using SchemaNode.Utility;
// ReSharper disable UnusedAutoPropertyAccessor.Global

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

        ISchemaApiProtocol apiProtocol = SchemaContext.GetRequiredService<ISchemaApiProtocol>();
        var protocolMeta = apiProtocol.GetProtocolMeta(SchemaContext.Services);
        
        return new ProtocolResponse
        {
            Name = protocolMeta.Name,
            Request = protocolMeta.Request?.ToJsonNode(),
            Response = protocolMeta.Response?.ToJsonNode(),
            SchemaFormat = protocolMeta.SchemaFormat
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
    /// <summary>
    /// The protocol name
    /// </summary>
    public string? Name { get; init; }
    
    /// <summary>
    /// The request schema
    /// </summary>
    public JsonNode? Request { get; init; } 
    
    /// <summary>
    /// The response schema
    /// </summary>
    public JsonNode? Response { get; init; }

    /// <summary>
    /// The supported schema formats for download
    /// </summary>
    public string[]? SchemaFormat { get; init; }
}