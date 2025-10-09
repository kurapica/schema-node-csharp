using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Http;
using SchemaNode.Node;
using SchemaNode.Schema;

namespace SchemaNode.Api.Schema.Application;

/// <summary>
/// The PushAppData api
/// </summary>
public class PushAppDataApi : SchemaApi<PushAppDataRequest, PushAppDataResponse>
{
    /// <inheritdoc />
    protected override async Task<PushAppDataResponse?> ExecuteAsync(PushAppDataRequest request,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]PushAppData [Request]{request}", request);
        
        (bool result, JsonNode? error) = await SchemaContext.PushAppDataAsync(request.App, request.Target, request.Datas);
        
        return new PushAppDataResponse
        {
            Result = await SchemaContext.PushAppDataAsync(request.Schema)
        };
    }
}

/// <summary>
/// The push data extension when provide the push data api by project with authentication
/// </summary>
public static class PushDataExtenstion
{
    /// <summary>
    /// Push app data
    /// </summary>
    public static async Task<(bool Result, JsonNode? Error)> PushAppDataAsync(this SchemaContext context, string app, string? target,
        Dictionary<string, AppDataFieldPushQuery>? datas)
    {
        await Task.Yield();
        return (true, null);
    }
}

/// <summary>
/// The PushAppData request
/// </summary>
public class PushAppDataRequest : SchemaApiRequest
{
    /// <summary>
    /// The application
    /// </summary>
    [Required]
    public required string App { get; set; }

    /// <summary>
    /// The target
    /// </summary>
    public string? Target { get; set; }

    /// <summary>
    /// The push data
    /// </summary>
    public Dictionary<string, AppDataFieldPushQuery>? Datas { get; set; }
}

/// <summary>
/// The PushAppData response
/// </summary>
public class PushAppDataResponse : SchemaApiResponse
{
    /// <summary>
    /// The result
    /// </summary>
    public bool Result { get; set; }
    
    /// <summary>
    /// The error data
    /// </summary>
    public JsonNode? Error { get; set; }
}

public class AppDataFieldPushQuery
{
    /// <summary>
    /// The push data
    /// </summary>
    public JsonNode? Data { get; set; }
    
    /// <summary>
    /// The deleted data
    /// </summary>
    public JsonArray? Deletes { get; set; }
}