using Microsoft.Extensions.Logging;
using SchemaNode.Components.Provider;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Http;
using SchemaNode.Node;
using SchemaNode.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using System.Xml.Linq;

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
        
        (AppDataResult[] result, NodeSchema[]? schemas) = await SchemaContext.BatchQueryAppDataAsync(request.Queries);
        
        return new BatchQueryAppDataResponse
        {
            Results = result,
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
    
    public static async Task<(AppDataResult[] Result, NodeSchema[]? Schemas)> BatchQueryAppDataAsync(this SchemaContext context, AppDataQuery[] queries)
    {
        List<AppDataResult> results = [];
        NodeSchema root = new NodeSchema
        {
            Name = "",
            Type = SchemaType.Namespace,
            Schemas = []
        };
        rootEnumValueInfo.Value = new EnumValueInfo();
        foreach (AppDataQuery query in queries)
        {
            if (string.IsNullOrWhiteSpace(query.App)) continue;
            if (string.IsNullOrWhiteSpace(query.Target)) continue; // @TODO: allow standalone app
            AppNode? node = await context.GetAppNodeAsync(query.App);
            if (node == null) continue;

            if (!(query.NoSchema ?? false))
            {
                node.GetNodeSchemas(root);
            }

            // query fields
            List<AppFieldNode> fields = node.Fields?.Where(f => !(f.Disable ?? false) && !(f.Frontend ?? false)).ToList() ?? [];
            if (query.Fields is { Length: > 0 })
            {
                fields = fields.Where(f => query.Fields.Any(qf => qf.Equals(f.Name, StringComparison.OrdinalIgnoreCase))).ToList();
            }
            if (query.OnlyInput == true)
            {
                fields = fields.Where(f => string.IsNullOrEmpty(f.Func) && string.IsNullOrEmpty(f.SourceApp)).ToList();
            }
            else if (query.OnlyOutput == true)
            {
                fields = fields.Where(f => !string.IsNullOrEmpty(f.Func) && string.IsNullOrEmpty(f.SourceApp)).ToList();
            }

            if (fields.Count == 0) continue;

            Dictionary<string, JsonNode> datas = [];
            Dictionary<string, AppDataFieldInfo> infos = [];
            HashSet<string> enumsKeys = [];

            if (!(query.SchemaOnly ?? false))
            {
                foreach (AppFieldNode field in fields)
                {
                    AppDataFieldQuery? q = query.Querys != null && query.Querys.ContainsKey(field.Name) ? query.Querys[field.Name] : null;
                    (JsonNode? result, int total) = await context.GetFieldDataAsync(field, query.Target!,
                        q?.Filter, q?.Skip ?? 0, q?.Take ?? query.Take ?? 0, q?.Descend ?? query.Descend ?? false, q?.OrderBy);
                    if (result != null)
                    {
                        datas[field.Name] = result;
                        infos[field.Name] = new AppDataFieldInfo
                        {
                            Filter = q?.Filter,
                            OrderBy = q?.OrderBy,
                            Skip = q?.Skip ?? 0,
                            Take = q?.Take ?? query.Take ?? 0,
                            Descend = q?.Descend ?? query.Descend ?? false,
                            Total = total
                        };

                        if (!query.NoSchema ?? false)
                        {
                            await ScanEnumAccess(context, root, field.TypeNode!, enumsKeys, result);
                        }
                    }
                }
            }

            // result
            AppDataResult appResult = new AppDataResult { 
                App = query.App,
                Target = query.Target,
                Results = datas,
                Infos = infos,
                Schema = !(query.NoSchema ?? false) ? new AppSchema
                {
                    Name = node.Name,
                    Display = node.Display,
                    Desc = node.Desc,
                    Standalone = node.Standalone,
                    HasFields = node.Fields is { Count: > 0 },
                    Fields = node.Fields!.Select(p => (AppFieldSchema)p).ToArray(),
                    Relations = node.Relations?.Select(r => new StructFieldRelation
                    {
                        Field = !string.IsNullOrEmpty(r.DataField) ? $"{r.AppField}.{r.DataField}" : r.AppField,
                        Type = r.Type,
                        Func = r.Func,
                        Args = r.Args?.Select(a => new FunctionCallArgument
                        {
                            Name = !string.IsNullOrEmpty(a.DataField) ? $"{a.AppField}.{a.DataField}" : a.AppField,
                            Value = a.Value,
                        }).ToArray() ?? []
                    }).ToArray()
                } : null
            };
            results.Add(appResult);

        }
                
        return (results.ToArray(), root.Schemas);
    }

    static async Task ScanEnumAccess(SchemaContext context, NodeSchema root, AnySchemaNode type, HashSet<string> enumsKeys, JsonNode? value)
    {
        switch (type)
        {
            case EnumNode enumNode:
                if (value is JsonValue val)
                {
                    string key = $"{enumNode.Name}:{val.GetValue<string>()}";
                    if (!enumsKeys.Contains(key))
                    {
                        enumsKeys.Add(key);
                        EnumValueAccess[] access = await enumNode.LoadEnumAccessListAsync(context, val.GetValue<string>());

                        if (access.Length > 0)
                        {
                            string[] paths = enumNode.Name.Split('.', StringSplitOptions.RemoveEmptyEntries);
                            string fullPath = string.Empty;
                            NodeSchema parent = root;
                            for (int i = 0; i < paths.Length; i++)
                            {
                                string p = paths[i];
                                fullPath = string.IsNullOrWhiteSpace(fullPath) ? p : $"{fullPath}.{p}";

                                parent.Schemas ??= [];
                                NodeSchema? sub = parent.Schemas.FirstOrDefault(s => s.Name.Equals(fullPath, StringComparison.OrdinalIgnoreCase));
                                if (sub == null) return;
                                parent = sub;
                            }

                            if (parent.Type == SchemaType.Enum)
                            {
                                rootEnumValueInfo.Value!.SubList = parent.Enum!.Values;
                                rootEnumValueInfo.Value!.CombineAccessList(access);
                                rootEnumValueInfo.Value!.SubList = null;
                            }
                        }
                    }
                }
                break;
            case StructNode @struct:
                if (value is JsonObject obj)
                {
                    foreach (StructFieldConfig f in @struct.Fields)
                    {
                        JsonNode? v = obj[f.Name];
                        if (v != null)
                            await ScanEnumAccess(context, root, f.TypeNode!, enumsKeys, v);
                    }
                }
                break;

            case ArrayNode array:
                if (value is not JsonArray arr) return;

                if (array.ElementNode is StructNode eleStruct)
                {
                    foreach (JsonNode? v in arr)
                    {
                        if (v is JsonObject)
                            await ScanEnumAccess(context, root, eleStruct, enumsKeys, v);
                    }
                }
                else if(array.ElementNode is EnumNode eleEnum)
                {
                    foreach (JsonNode? v in arr)
                    {
                        if (v is JsonValue)
                            await ScanEnumAccess(context, root, eleEnum, enumsKeys, v);
                    }
                }
                break;
        }
    }

    static AsyncLocal<EnumValueInfo> rootEnumValueInfo = new();
}

/// <summary>
/// The BatchQueryAppData request
/// </summary>
public class BatchQueryAppDataRequest : SchemaApiRequest
{
    /// <summary>
    /// The app data queries    
    /// </summary>
    public AppDataQuery[] Queries { get; set; } = [];
}

/// <summary>
/// The BatchQueryAppData response
/// </summary>
public class BatchQueryAppDataResponse : SchemaApiResponse
{
    /// <summary>
    /// The result
    /// </summary>
    public AppDataResult[]? Results { get; set; }
    
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
    public int? Take { get; set; }
    
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