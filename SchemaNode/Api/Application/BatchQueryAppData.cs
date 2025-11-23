using Microsoft.Extensions.Logging;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Http;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;

// ReSharper disable UnusedAutoPropertyAccessor.Global

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
        
        (AppDataResult[] result, NodeSchema[]? schemas) = await SchemaContext.BatchQueryAppDataAsync(request.Queries, cancellationToken);
        
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
    
    public static async Task<(AppDataResult[] Result, NodeSchema[]? Schemas)> BatchQueryAppDataAsync(this SchemaContext context, AppDataQuery[] queries, CancellationToken? cancellationToken = null)
    {
        List<AppDataResult> results = [];
        NodeSchema root = new NodeSchema
        {
            Name = "",
            Type = SchemaType.Namespace,
            Schemas = []
        };
        RootEnumValueInfo.Value = new EnumValueInfo();
        foreach (AppDataQuery query in queries)
        {
            cancellationToken?.ThrowIfCancellationRequested();
            
            if (string.IsNullOrWhiteSpace(query.App)) continue;
            if (string.IsNullOrWhiteSpace(query.Target)) continue;
            AppType? node = await context.GetAppTypeAsync(query.App);
            if (node == null) continue;

            // authorize
            await context.AuthorizeAsync(node, PolicyScope.SchemaRead);
            
            // load schema
            if (!(query.NoSchema ?? false)) await node.GetNodeSchemas(context, root, cancellationToken:cancellationToken);

            // query fields
            List<AppFieldType> fields = node.Fields?.Where(f => f.IsQueryable).ToList() ?? [];
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
                foreach (AppFieldType field in fields)
                {
                    // authorize field
                    if (!await context.AuthorizeAsync(field, PolicyScope.DataRead, true)) continue;
                    
                    // prepare field query
                    AppDataFieldQuery? q = query.Querys != null && query.Querys.TryGetValue(field.Name, out var queryQuery) ? queryQuery : null;
                    
                    // limit incr field take count
                    int take = q?.Take ?? query.Take ?? 0;
                    if (field.IncrUpdate == true)
                    {
                        take = take <= 0 
                            ? SchemaContext.Config.IncrFieldDefaultTakeCount 
                            : Math.Min(take, SchemaContext.Config.IncrFieldMaxTakeCount);
                    }
                    else
                    {
                        take = 0;
                    }
                    
                    // row access check
                    ExpNode? rowFilter = null;
                    if (field.SchemaType is ArrayType { ElementSchemaType: StructType structType })
                    {
                        PolicyItem[] rowAccess = field.GetAuthPolicies(field.Name, PolicyScope.RowAccess).ToArray();
                        if (rowAccess.Length > 0)
                        {
                            for (int i = rowAccess.Length - 1; i >= 0; i--)
                            {
                                try
                                {
                                    FunctionType func = rowAccess[i].Function ?? throw new InvalidOperationException();
                                    
                                    // check type
                                    if (func.Args.Length != 1 
                                        || func.Args[0].TypeNode == null 
                                        || !func.Args[0].TypeNode!.CanBeUseAs(structType)) 
                                        continue;

                                    // visite the function exp tree for where clause
                                    rowFilter = await RowAccessExpTreeVisitor.Visit(context, func, q?.Filter);
                                    break;
                                }
                                catch
                                {
                                    rowFilter = null;
                                }
                            }
                            // get nothing 
                            if (rowFilter == null) continue;
                        }
                    }
                    
                    // Set the current access
                    context.SetAppAccess(field.App, query.Target, field.Name);

                    // query app field data
                    (AnySchemaNode? result, int total) = rowFilter != null
                        ? await context.GetFieldDataAsync(field, query.Target!, rowFilter, q?.Skip ?? 0, take, q?.Descend ?? query.Descend ?? false, q?.OrderBy)
                        : await context.GetFieldDataAsync(field, query.Target!, q?.Filter, q?.Skip ?? 0, take, q?.Descend ?? query.Descend ?? false, q?.OrderBy);
                    
                    // mark loaded
                    infos[field.Name] = new AppDataFieldInfo
                    {
                        Filter = q?.Filter,
                        OrderBy = q?.OrderBy,
                        Skip = q?.Skip ?? 0,
                        Take = take,
                        Descend = q?.Descend ?? query.Descend ?? false,
                        Total = total
                    };

                    // cover result
                    if (result != null)
                    {
                        datas[field.Name] =  result.ToJson()!;
                        
                        // column access check
                        var @struct = result switch
                        {
                            ArrayTypeNode arr => arr.ElementType as StructType,
                            StructTypeNode st => st.Type as StructType,
                            _ => null
                        };
                        if (@struct != null)
                        {
                            List<string>? ignoreFields = null;
                            foreach (StructFieldConfig f in @struct.Fields)
                            {
                                try
                                {
                                    await context.AuthorizeAsync(field, f.Name, PolicyScope.ColumnAccess);
                                }
                                catch
                                {
                                    ignoreFields ??= [];
                                    ignoreFields.Add(f.Name);
                                }
                            }

                            // remove ignore fields
                            if (ignoreFields != null)
                            {
                                if (datas[field.Name] is JsonArray jsonArray)
                                {
                                    foreach(var obj in jsonArray)
                                    {
                                        if (obj is not JsonObject jsonObj) continue;
                                        foreach (string ig in ignoreFields)
                                        {
                                            jsonObj.Remove(ig);
                                        }
                                    }
                                }
                                else if (datas[field.Name] is JsonObject jsonObject)
                                {
                                    foreach (string ig in ignoreFields)
                                    {
                                        jsonObject.Remove(ig);
                                    }
                                }
                            }
                        }
                        
                        // scan enum access
                        if (!query.NoSchema ?? false)
                            await ScanEnumAccess(context, root, field.SchemaType!, enumsKeys, result);
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
                    HasFields = node.Fields is { Count: > 0 },
                    Fields = node.Fields!.Select(p => (AppFieldSchema)p).ToArray(),
                    Relations = node.Relations?.Select(r => new StructFieldRelation
                    {
                        Field = !string.IsNullOrEmpty(r.DataField) ? $"{r.AppField}.{r.DataField}" : r.AppField,
                        Type = r.Type,
                        Func = r.Func,
                        Args = r.Args.Select(a => new FuncCallArg
                        {
                            Name = !string.IsNullOrEmpty(a.DataField) ? $"{a.AppField}.{a.DataField}" : a.AppField,
                            Value = a.Value,
                        }).ToArray()
                    }).ToArray()
                } : null
            };
            results.Add(appResult);

        }
                
        return (results.ToArray(), root.Schemas);
    }

    static async Task ScanEnumAccess(SchemaContext context, NodeSchema root, AnySchemeType type, HashSet<string> enumsKeys, AnySchemaNode? value)
    {
        switch (type)
        {
            case EnumType enumNode:
                if (value is EnumTypeNode val)
                {
                    string key = $"{enumNode.Name}:{val.ToValue<string>()}";
                    if (enumsKeys.Add(key))
                    {
                        EnumValueAccess[] access = await enumNode.LoadEnumAccessListAsync(context, val.ToValue<string>()!);

                        if (access.Length > 0)
                        {
                            string[] paths = enumNode.Name.Split('.', StringSplitOptions.RemoveEmptyEntries);
                            string fullPath = string.Empty;
                            NodeSchema parent = root;
                            foreach (string p in paths)
                            {
                                fullPath = string.IsNullOrWhiteSpace(fullPath) ? p : $"{fullPath}.{p}";

                                parent.Schemas ??= [];
                                NodeSchema? sub = parent.Schemas.FirstOrDefault(s => s.Name.Equals(fullPath, StringComparison.OrdinalIgnoreCase));
                                if (sub == null) return;
                                parent = sub;
                            }

                            if (parent.Type == SchemaType.Enum)
                            {
                                RootEnumValueInfo.Value!.SubList = parent.Enum!.Values;
                                RootEnumValueInfo.Value!.CombineAccessList(access);
                                RootEnumValueInfo.Value!.SubList = null;
                            }
                        }
                    }
                }
                break;
            case StructType @struct:
                if (value is StructTypeNode obj)
                {
                    foreach (StructFieldConfig f in @struct.Fields)
                    {
                        AnySchemaNode? v = obj.GetField(f.Name);
                        if (v is { IsEmpty: false })
                            await ScanEnumAccess(context, root, f.TypeNode!, enumsKeys, v);
                    }
                }
                break;

            case ArrayType array:
                if (value is not ArrayTypeNode arr) return;

                switch (array.ElementSchemaType)
                {
                    case StructType eleStruct:
                    {
                        foreach (AnySchemaNode v in arr)
                        {
                            if (v is StructTypeNode)
                                await ScanEnumAccess(context, root, eleStruct, enumsKeys, v);
                        }

                        break;
                    }
                    case EnumType eleEnum:
                    {
                        foreach (AnySchemaNode v in arr)
                        {
                            if (v is EnumTypeNode)
                                await ScanEnumAccess(context, root, eleEnum, enumsKeys, v);
                        }

                        break;
                    }
                }
                break;
        }
    }

    static readonly AsyncLocal<EnumValueInfo> RootEnumValueInfo = new();
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
    /// The default take count for incr update field
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
/// The query field result info
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