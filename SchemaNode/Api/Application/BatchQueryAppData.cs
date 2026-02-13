using Microsoft.Extensions.Logging;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Http;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

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
            
            // set access
            context.SetAccess(node.Name, query.Target); 

            // load schema
            if (!(query.NoSchema ?? false)) await node.GetNodeSchemas(context, root, cancellationToken:cancellationToken);

            // query fields
            IEnumerable<AppFieldType> fields = node.Fields?.Where(f => f.IsQueryable) ?? [];
            fields = query.Fields is { Length: > 0 }
                ? fields.Where(f => query.Fields.Any(qf => qf.Equals(f.Name, StringComparison.OrdinalIgnoreCase)))
                : fields;
            
            // filter input/output fields
            if (query.OnlyInput == true)
                fields = fields.Where(f => string.IsNullOrEmpty(f.Func) && string.IsNullOrEmpty(f.SourceApp));
            else if (query.OnlyOutput == true)
                fields = fields.Where(f => !string.IsNullOrEmpty(f.Func) && string.IsNullOrEmpty(f.SourceApp));

            // result
            Dictionary<string, JsonNode> fieldResults = [];
            Dictionary<string, AppDataFieldInfo> fieldInfos = [];
            HashSet<string> enumsKeys = [];

            if (!(query.SchemaOnly ?? false))
            {
                foreach (AppFieldType field in fields)
                {
                    AnySchemaNode? result = null;
                    int total = 0;

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

                    // authorize field
                    bool allowRead = await context.AuthorizeAsync(field, PolicyScope.DataRead, true);

                    // filter func
                    AppSchemaDataFilter? filter = null;
                    
                    if (allowRead)
                    {
                        if (!string.IsNullOrWhiteSpace(q?.FilterFunc))
                        {
                            // filter func not valid, deny read
                            if (await context.GetSchemaTypeAsync(q.FilterFunc) is not FunctionType filterFunc) continue;
                            
                            // Call filter func with policy filter compile context
                            filter = await filterFunc.CallAsync<AppSchemaDataFilter, RefFilterCompileContext>(context, q.FilterArgsArray?.Select(object? (p) => p).ToArray() ?? []);
                        }

                        // row access check
                        if (field is { SchemaType: ArrayType { ElementSchemaType: StructType structType }, RowAuths.Length: > 0 })
                        {
                            bool authorized = true;
                            foreach (RowPolicyItem policy in field.RowAuths)
                            {
                                try
                                {
                                    // Authorize evaluator
                                    authorized = await context.AuthorizeAsync(policy.Evaluator, true);
                                    if (!authorized) continue;
                                    if (policy.FilterFunc == null) break;

                                    // check type
                                    if (policy.FilterFunc.Args.Length != 1
                                        || policy.FilterFunc.Args[0].SchemaType == null
                                        || !policy.FilterFunc.Args[0].SchemaType!.CanBeUseAs(structType))
                                    {
                                        authorized = false;
                                        continue;
                                    }

                                    // Call filter func with policy filter compile context
                                    AppSchemaDataFilter? f = await policy.FilterFunc.CallAsync<AppSchemaDataFilter, QueryFilterCompileContext>(context, []);
                                    if (f == null)
                                    {
                                        authorized = false;
                                        continue;
                                    }

                                    filter = filter == null ? f : filter.AndAlso(f);
                                    break;
                                }
                                catch (Exception e)
                                {
                                    context.Logger.LogError(e, $"BatchQueryAppDataAsync row access check error for func ${policy.Evaluator}");
                                    authorized = false;
                                }
                            }
                            allowRead = authorized;
                        }

                        if (allowRead)
                        {
                            // Combine filters
                            if (q?.Filter != null)
                            {
                                var qFilter = await q.Filter.ToAppSchemaDataFilterAsync(context, ((field.SchemaType as ArrayType)!.ElementSchemaType as StructType)!, field.Filters);
                                filter = filter != null && qFilter != null ? filter.AndAlso(qFilter) : (filter ?? qFilter);
                            }
                            
                            // Validate and transform filter
                            bool isValidFilter = filter == null;
                            if (filter != null)
                            {
                                isValidFilter = filter.Transform(out AppSchemaDataFilter? final);
                                filter = final;

                                // Avoid invalid filter types like false means no data
                                if (isValidFilter && filter is AppSchemaDataFilterValue or AppSchemaDataFilterField)
                                    isValidFilter = false;
                            }
                            
                            if (isValidFilter)
                                (result, total) = await context.GetFieldDataAsync( field, query.Target!, AppSchemaDataResult.List,
                                    filter, q?.Skip ?? 0, take, q?.Descend ?? query.Descend ?? false, q?.OrderBy, genDisplayOnly:true);
                        }
                    }
                    
                    // mark loaded
                    fieldInfos[field.Name] = new AppDataFieldInfo
                    {
                        Filter = filter?.ToFilter() ?? q?.Filter,
                        OrderBy = q?.OrderBy,
                        Skip = q?.Skip ?? 0,
                        Take = take,
                        Descend = q?.Descend ?? query.Descend ?? false,
                        Total = total,
                        FilterFunc = q?.FilterFunc,
                        FilterArgs = q?.FilterArgsArray?.DeepClone() as JsonArray,
                        AllowRead = allowRead,
                        AllowCreate = await context.AuthorizeAsync(field, PolicyScope.DataCreate, true),
                        AllowUpdate = await context.AuthorizeAsync(field, PolicyScope.DataUpdate, true),
                        AllowDelete = await context.AuthorizeAsync(field, PolicyScope.DataDelete, true),
                    };

                    // cover result
                    if (result != null)
                    {
                        fieldResults[field.Name] =  result.ToJson()!;
                        
                        // column access check
                        var @struct = result switch
                        {
                            ArrayTypeNode arr => arr.ElementType as StructType,
                            StructTypeNode st => st.SchemaType as StructType,
                            _ => null
                        };
                        if (@struct != null)
                        {
                            List<string>? ignoreFields = null;
                            foreach (StructFieldConfig f in @struct.Fields)
                            {
                                // Authorize with order
                                bool authorized = true;
                                foreach(string evaluator in field.GetColPolicies(f.Name))
                                {
                                    authorized = await context.AuthorizeAsync(evaluator, true);
                                    if (authorized) break;
                                }
                                if (authorized) continue;

                                ignoreFields ??= [];
                                ignoreFields.Add(f.Name);
                            }

                            // remove ignore fields
                            if (ignoreFields != null)
                            {
                                fieldInfos[field.Name].BlackColumns = ignoreFields.ToArray();
                                
                                if (fieldResults[field.Name] is JsonArray jsonArray)
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
                                else if (fieldResults[field.Name] is JsonObject jsonObject)
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
                Results = fieldResults,
                Infos = fieldInfos,
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
                    }).ToArray(),
                    Workflows = node.Workflows?.Select(w => (AppWorkflowSchema)w).ToArray(),
                } : null
            };
            
            // workflow states
            if (query.Workflow == true && node.Workflows is { Count: > 0 })
            {
                List<AppWorkflowState> workflows = [];
                foreach (AppWorkflowType wf in node.Workflows)
                {
                    if (wf.Nodes.Length == 0 || wf.RootWorkflowContext?.EntryWorkflow == null ||
                        !await context.AuthorizeAsync(wf, PolicyScope.FuncExecute, true)) continue;
                    Workflow firstNode = wf.RootWorkflowContext.EntryWorkflow;
                    
                    // Only show activated interaction workflow
                    if (firstNode is not InteractionWorkflow interWorkflow) continue;
                    
                    // Check if only allow one workflow context
                    Guid? workflowId = null;
                    bool togglable = false;
                    if (interWorkflow is { Fork: true, CancelPre: false, ForkKey.Length: > 0 } && 
                        interWorkflow.ForkKey.Contains(nameof(InteractionPayload.Target), StringComparer.OrdinalIgnoreCase))
                    {
                        togglable = true;
                        foreach (WorkflowContext forkContext in wf.RootWorkflowContext.GetForkedWorkflowContexts(firstNode))
                        {
                            StructTypeNode? payload = forkContext.GetWorkflowPayload(firstNode) as StructTypeNode;
                            string? target = payload?.GetField(nameof(InteractionPayload.Target))?.ToValue<string>();
                            if (target != null && target.Equals(query.Target, StringComparison.OrdinalIgnoreCase))
                            {
                                workflowId = forkContext.Id;
                                break;
                            }
                        }
                    }
                    workflows.Add(new AppWorkflowState
                    {
                        Name = wf.Name,
                        Togglable = togglable,
                        WorkflowId = workflowId,
                    });
                }
                appResult.Workflows = workflows.Count > 0 ? workflows.ToArray() : null;
            }
            
            results.Add(appResult);

            // raise event
            context.RaiseEvent(new AppDataReadEvent(node.Name, query.Target));
        }
        
        return (results.ToArray(), root.Schemas);
    }

    static async Task ScanEnumAccess(SchemaContext context, NodeSchema root, AnySchemaType type, HashSet<string> enumsKeys, AnySchemaNode? value)
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
                            await ScanEnumAccess(context, root, f.SchemeType!, enumsKeys, v);
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
    
    /// <summary>
    /// Query the interaction workflow data
    /// </summary>
    public bool? Workflow { get; set; }
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
    
    /// <summary>
    /// The filter function
    /// </summary>
    public string? FilterFunc { get; set; }

    /// <summary>
    /// The filter function args
    /// </summary>
    public JsonElement? FilterArgs { get; set; }
    
    [JsonIgnore]
    public JsonArray? FilterArgsArray => FilterArgs is { ValueKind: JsonValueKind.Array }
        ? JsonNode.Parse(FilterArgs.Value.GetRawText()) as JsonArray
        : null;
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
    
    /// <summary>
    /// The interaction workflows states
    /// </summary>
    public AppWorkflowState[]? Workflows { get; set; }
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
    
    /// <summary>
    /// Allow create
    /// </summary>
    public bool AllowCreate { get; set; }
    
    /// <summary>
    /// Allow read
    /// </summary>
    public bool AllowRead { get; set; }
    
    /// <summary>
    /// Allow update
    /// </summary>
    public bool AllowUpdate { get; set; }
    
    /// <summary>
    /// Allow delete
    /// </summary>
    public bool AllowDelete { get; set; }
    
    /// <summary>
    /// Disable columns access
    /// </summary>
    public string[]? BlackColumns { get; set;  }

    /// <summary>
    /// The filter func
    /// </summary>
    public string? FilterFunc { get; set; }

    /// <summary>
    ///  The filter args
    /// </summary>
    public JsonArray? FilterArgs { get; set; }
}

/// <summary>
/// The interaction workflow state
/// </summary>
public class AppWorkflowState
{
    /// <summary>
    /// The interaction workflow name
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// If the workflow is togglable
    /// </summary>
    public bool Togglable { get; set; }

    /// <summary>
    /// The activated workflow ID
    /// </summary>
    public Guid? WorkflowId { get; set; }
}