using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using SchemaNode.Context;
using SchemaNode.Http;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Utility;

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
            Result = result,
            Error = error
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
        if (string.IsNullOrWhiteSpace(app)) return (false, Constant.APP_NOT_FOUND);
        if (string.IsNullOrWhiteSpace(target)) return (false, Constant.APP_TARGET_REQUIRED);
        if (datas == null || datas.Count == 0) return (false, Constant.APP_PUSH_DATA_REQUIRED);

        AppType? appNode = await context.GetAppNodeAsync(app);
        if (appNode == null) return (false, Constant.APP_NOT_FOUND);

        bool hasData = false;

        foreach((string field, AppDataFieldPushQuery push) in datas)
        {
            AppFieldType? appField = appNode.Fields?.FirstOrDefault(f => f.Name.Equals(field, StringComparison.OrdinalIgnoreCase));
            if (appField == null) continue;

            if (!hasData)
            {
                hasData = true;
                await context.BeginTransactionAsync();
            }

            if (push.Data != null)
            {
                (_, AnySchemaNode? result, JsonNode? error) = await appField.ValidateDataAsync(context, push.Data);
                if (error != null) return (false, error);
                await context.SaveFieldDataAsync(appField, target, result);
            }
            
            if (push.Deletes is { Count: > 0 })
            {
                await context.DeleteFieldListDataAsync(appField, target, push.Deletes);
            }
        }

        if (hasData)
            await context.CommitTransactionAsync();
        
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