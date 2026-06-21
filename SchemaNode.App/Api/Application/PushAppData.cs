using Microsoft.Extensions.Logging;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Http;
using SchemaNode.Node;
using SchemaNode.Runtime;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using SchemaNode.Data;
using SchemaNode.Property.App;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
using ArrayType = SchemaNode.Runtime.ArrayType;
using StructType = SchemaNode.Runtime.StructType;

// ReSharper disable UnusedAutoPropertyAccessor.Global

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
        Logger.LogDebug("[Api]PushAppData [Request]{app} - {target}", request.App, request.Target);
        
        using var criticalRegion = await GetLockAsync("PushAppData:{0}{1}", request.App, request.Target ?? Guid.Empty.ToString());
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
        Dictionary<string, AppDataFieldPushQuery>? data)
    {
        if (string.IsNullOrWhiteSpace(app)) return (false, AppErrorCodes.APP_NOT_FOUND);
        if (data == null || data.Count == 0) return (false, AppErrorCodes.APP_PUSH_DATA_REQUIRED);

        Runtime.AppType? appNode = await context.GetAppTypeAsync(app);
        if (appNode == null) return (false, AppErrorCodes.APP_NOT_FOUND);

        // target is required for non-system-level apps
        if (appNode.ScopeType != AppScopeType.SystemLevel && string.IsNullOrWhiteSpace(target))
            return (false, AppErrorCodes.APP_TARGET_REQUIRED);

        // set access
        context.SetAccess(appNode.Name, target);
        
        bool hasData = false;
        try
        {
            foreach((string field, AppDataFieldPushQuery push) in data)
            {
                AppFieldType? appField = appNode.GetField(field);
                if (appField == null) continue;
            
                // authorize
                await context.AuthorizeAsync(appField, PolicyScope.DataUpdate);
                bool canAdd = await context.AuthorizeAsync(appField, PolicyScope.DataCreate, true);
                bool canDel = await context.AuthorizeAsync(appField, PolicyScope.DataDelete, true);

                // no permission to delete data
                if (!canDel && push.Deletes is { Count: > 0 })
                    throw new UnauthorizedAccessException();
                
                // Check clear all
                if (push.ClearAll == true && (!canDel || appField.GetProperty<AllowClear>() is not { Value: true }))
                    throw new UnauthorizedAccessException();

                // row access check
                FunctionType? rowChecker = null;
                if (appField is {  ValueType: ArrayType {  Element: StructType structType } } && appField.GetProperty<RowAuths>() is { Value: { Length: > 0}} rowAuths)
                {
                    bool authorized = true;
                    foreach (RowPolicy policy in rowAuths.Value)
                    {
                        try
                        {
                            // Authorize evaluator
                            authorized = await context.AuthorizeAsync(policy.Evaluator, true);
                            if (!authorized) continue;
                            if (policy.FilterFunc == null) break;

                            // check type
                            if (policy.FilterFunc.Args.Length != 1
                                || policy.FilterFunc.Args[0].ValueType == null
                                || !policy.FilterFunc.Args[0].ValueType!.IsAssignableTo(structType))
                            {
                                authorized = false;
                                continue;
                            }

                            // visite the function exp tree for where clause
                            rowChecker = policy.FilterFunc;
                            break;
                        }
                        catch (Exception e)
                        {
                            context.LogError(e, $"PushAppDataAsync row access check error for func ${policy.Evaluator}");
                            rowChecker = null;
                        }
                    }

                    if (rowChecker != null)
                    {
                        // check data row access permission
                        if (push.Data is JsonArray arr)
                        {
                            foreach (JsonNode? item in arr)
                                await ValidateRow(context, rowChecker, item);
                        }
                        else
                            await ValidateRow(context, rowChecker, push.Data);

                        if (push.Deletes is { Count: > 0 })
                        {
                            foreach (JsonNode? item in push.Deletes)
                                await ValidateRow(context, rowChecker, item);
                        }
                    }
                    else if(!authorized)
                    {
                        throw new UnauthorizedAccessException();
                    }
                }

                // begin transaction if have data
                if (!hasData)
                {
                    hasData = true;
                    await context.BeginTransactionAsync();
                }

                if (push.ClearAll == true)
                {
                    await context.ClearFieldDataAsync(appField);
                    continue;
                }

                // validate and save data
                if (push.Data != null)
                {
                    DataNode? result = await appField.ValidateDataAsync(context, push.Data);
                    if (result is not { IsValid: true })
                    {
                        if (hasData) await context.RollbackTransactionAsync();
                        return (false, result?.ToJsonNode());
                    }
                    await context.SaveFieldDataAsync(appField, result, canAdd: canAdd);
                }

                if (push.Deletes is { Count: > 0 })
                {
                    DataNode? result = await appField.ValidateDataAsync(context, push.Deletes);
                    if (result is not { IsValid: true })
                    {
                        if (hasData) await context.RollbackTransactionAsync();
                        return (false, result?.ToJsonNode());
                    }
                    await context.DeleteFieldListDataAsync(appField, result);
                }
            }

            if (hasData)
                await context.CommitTransactionAsync();
        }
        catch(Exception)
        {
            if (hasData) 
                await context.RollbackTransactionAsync();
            throw;
        }

        return (true, null);
    }

    static async Task ValidateRow(SchemaContext context, FunctionType rowChecker, JsonNode? item)
    {
        if (item is not JsonObject) throw new UnauthorizedAccessException();
        try
        {
            var args = new JsonArray(1)
            {
                [0] = item.DeepClone()
            };
            var res = await context.CallFunctionAsync(rowChecker, args, NS_SYSTEM_BOOL);
            if (res is not JsonValue boolVal || !boolVal.TryGetValue(out bool allowed) || !allowed)
                throw new UnauthorizedAccessException();
        }
        catch
        {
            throw new UnauthorizedAccessException();
        }
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
    /// The target (not required for system-level scope apps)
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
    
    /// <summary>
    /// Clear all existing data for the field
    /// </summary>
    public bool? ClearAll { get; set; }
}