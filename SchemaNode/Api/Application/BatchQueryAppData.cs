using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using SchemaNode.Components.Provider;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Http;
using SchemaNode.Node;
using SchemaNode.Schema;

namespace SchemaNode.Api.Schema.Application;

/// <summary>
/// The BatchQueryAppData api
/// </summary>
public class BatchQueryAppDataApi : SchemaApi<BatchQueryAppDataRequest, BatchQueryAppDataResponse>
{
    /// <inheritdoc />
    protected override async Task<BatchQueryAppDataResponse?> ExecuteAsync(BatchQueryAppDataRequest request,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]BatchQueryAppData [Request]{request}", request);
        
        (AppDataResult[] result, NodeSchema[]? schemas) = await SchemaContext.BatchQueryAppDataAsync(request.Querys);
        
        return new BatchQueryAppDataResponse
        {
            Result = result,
            Schemas = schemas
        };
    }
}

/// <summary>
/// The batch query extension when provide the batch query api by project with authentication
/// </summary>
public static class BatchQueryExtension
{
    /// <summary>
    /// Batch query app data with schemas
    /// </summary>
    public static async Task<(AppDataResult[] Result, NodeSchema[]? Schemas)> BatchQueryAppDataAsync(this SchemaContext context, AppDataQuery[] querys)
    {
        List<AppDataResult> results = [];
        HashSet<string> schemaIds = [];
        foreach (AppDataQuery query in querys)
        {
            if (string.IsNullOrWhiteSpace(query.App)) continue;
            AppNode? node = await context.GetAppNodeAsync(query.App);
            if (node == null) continue;
            var (result, schemaId) = await context.QueryAppDataAsync(node, query);
            if (result != null)
            {
                results.Add(result);
                if (!string.IsNullOrWhiteSpace(schemaId))
                {
                    schemaIds.Add(schemaId);
                }
            }
        }

        NodeSchema[]? schemas = null;
        if (schemaIds.Count > 0)
        {
            schemas = await context.GetNodeSchemasAsync(schemaIds.ToArray());
        }
        
        return (results.ToArray(), schemas);
    }
}

/// <summary>
/// The BatchQueryAppData request
/// </summary>
public class BatchQueryAppDataRequest : SchemaApiRequest
{
    /// <summary>
    /// The app data queries    
    /// </summary>
    public AppDataQuery[] Querys { get; set; }
}

/// <summary>
/// The BatchQueryAppData response
/// </summary>
public class BatchQueryAppDataResponse : SchemaApiResponse
{
    /// <summary>
    /// The result
    /// </summary>
    public AppDataResult[] Result { get; set; }
    
    /// <summary>
    /// The node schemas used by apps
    /// </summary>
    public NodeSchema[]? Schemas { get; set; }
}

/// <summary>
/// The app data query
/// </summary>
public class AppDataQuery
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
    /// The query fields, empty means all fields
    /// </summary>
    public string[]? Fields { get; set; }
    
    /// <summary>
    /// Only query input fields
    /// </summary>
    public bool? OnlyInput { get; set; }

    /// <summary>
    /// Only query output fields
    /// </summary>
    public bool? OnlyOutput { get; set; }
    
    /// <summary>
    /// The queries
    /// </summary>
    public Dictionary<string, AppDataFieldQuery>? Querys { get; set; }
    
    /// <summary>
    /// The default take count
    /// </summary>
    public int? Task { get; set; }
    
    /// <summary>
    /// The default order
    /// </summary>
    public bool? Descend { get; set; }
    
    /// <summary>
    /// Only query the schema without data
    /// </summary>
    public bool? SchemaOnly { get; set; }
    
    /// <summary>
    /// Only query the data without schema
    /// </summary>
    public bool? NoSchema { get; set; }
}

public class AppDataFieldQuery
{
    /// <summary>
    /// The filter, only primary key supported
    /// </summary>
    public JsonObject? Filter { get; set; }
    
    /// <summary>
    /// The order by details
    /// </summary>
    public AppSchemaDataOrder[]? OrderBy { get; set; }
    
    /// <summary>
    /// Skip count
    /// </summary>
    public int? Skip { get; set; }
    
    /// <summary>
    /// Take count
    /// </summary>
    public int? Take { get; set; }
    
    /// <summary>
    /// Use descent order
    /// </summary>
    public bool? Descend { get; set; }
}

public class AppDataResult
{
    /// <summary>
    /// The application
    /// </summary>
    public required string App { get; set; }
    
    /// <summary>
    /// The target
    /// </summary>
    public string? Target { get; set; }
    
    /// <summary>
    /// The application schema
    /// </summary>
    public AppSchema? Schema { get; set; }
    
    /// <summary>
    /// The app field data
    /// </summary>
    public Dictionary<string, JsonNode>? Results { get; set; }
    
    /// <summary>
    /// The query infos
    /// </summary>
    public Dictionary<string, AppDataFieldInfo>? Infos { get; set; }
}

/// <summary>
/// The queryfield result info
/// </summary>
public class AppDataFieldInfo
{
    /// <summary>
    /// The filter, only primary key supported
    /// </summary>
    public JsonObject? Filter { get; set; }
    
    /// <summary>
    /// The order by details
    /// </summary>
    public AppSchemaDataOrder[]? OrderBy { get; set; }
    
    /// <summary>
    /// Skip count
    /// </summary>
    public int? Skip { get; set; }
    
    /// <summary>
    /// Take count
    /// </summary>
    public int? Take { get; set; }
    
    /// <summary>
    /// Use descent order
    /// </summary>
    public bool? Descend { get; set; }
    
    /// <summary>
    /// The total count
    /// </summary>
    public int? Total { get; set; }
}