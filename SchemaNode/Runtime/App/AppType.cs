using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory application schema representation
/// </summary>
public class AppType
{
    #region Properties

    /// <summary>
    /// The application name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The display name
    /// </summary>
    public LocaleString? Display { get; private set; }

    /// <summary>
    /// The description
    /// </summary>
    public LocaleString? Desc { get; private set; }
    
    /// <summary>
    /// The authentication policy type
    /// </summary>
    public PolicyType? Auth { get; set; }
    
    /// <summary>
    /// The data authentication policy type
    /// </summary>
    public PolicyItem[]? Auths { get; set; }

    /// <summary>
    /// The application field relations
    /// </summary>
    public List<AppRelationSchema>? Relations { get; private set; }

    /// <summary>
    /// The sub applications
    /// </summary>
    public AppSchema[]? Apps { get; internal set; }

    /// <summary>
    /// The additional data
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; internal set; }
    
    /// <summary>
    /// The root application
    /// </summary>
    public AppType? RootApp { get; set; }

    #endregion

    #region State

    /// <summary>
    /// The application node status
    /// </summary>
    public SchemaNodeStatus Status => Fields is { Count: > 0 } && Fields.Any(p => p.Status != null && p.Status != SchemaNodeStatus.Ready)
        ? SchemaNodeStatus.ApplicationInvalidField
        : Auths != null && Auths.Any(p => p.Status != null && p.Status != SchemaNodeStatus.Ready)
            ? SchemaNodeStatus.ApplicationDataAuthWrongFunc
            : SchemaNodeStatus.Ready;

    /// <summary>
    /// The application is used
    /// </summary>
    public bool IsUsed => Fields is { Count: > 0 } || Apps is { Length: > 0 };
    
    /// <summary>
    /// Already loaded
    /// </summary>
    internal bool Loaded { get; set; }

    #endregion

    #region Relationship

    /// <summary>
    /// The sub application node
    /// </summary>
    public ConcurrentDictionary<string, AppType>? SubAppList { get; set; }

    /// <summary>
    /// The application field nodes
    /// </summary>
    public List<AppFieldType>? Fields { get; set; }
    
    /// <summary>
    /// The application workflows
    /// </summary>
    public List<AppWorkflowType>? Workflows { get; set; }

    /// <summary>
    /// The ref field
    /// </summary>
    public AppFieldType? RefField { get; set; }

    #endregion

    #region Methods

    /// <summary>
    /// Load the data
    /// </summary>
    public async Task LoadAsync(SchemaContext context, AppSchema schema, bool preLoad = false)
    {
        // Release old usages
        Release();

        // data
        Display = schema.Display;
        Desc = schema.Desc;
        Auth = !string.IsNullOrEmpty(schema.Auth)
            ? await context.GetSchemaTypeAsync(schema.Auth) as PolicyType
            : null;
        Auths = schema.Auths;
        Apps = schema.Apps;
        Additional = schema.Additional;

        // Load the application fields
        bool useRef = false;
        bool requireDb = false;
        Fields = schema.Fields?.Select(p => (AppFieldType)p).ToList();
        Relations = null;
        if (Fields is { Count: > 0 })
        {
            // load field type first to avoid circular reference
            foreach (AppFieldType field in Fields)
            {
                field.App = Name;
                field.Application = this;
                field.Status = null;

                // valid the type
                AnySchemaType? node = await context.GetSchemaTypeAsync(field.Type);
                if (node == null)
                    field.Status = SchemaNodeStatus.ApplicationFieldWrongType;
                else
                {
                    node.AddRef(field);
                    field.SchemaType = node;
                }
            }

            // load field details
            foreach (AppFieldType field in Fields)
            {
                // valid the push function
                if (!string.IsNullOrWhiteSpace(field.Func))
                {
                    AnySchemaType? node = await context.GetSchemaTypeAsync(field.Func);
                    if (node is FunctionType { Args.Length: 1 } funcNode)
                    {
                        field.FuncNode = funcNode;
                        funcNode.AddRef(field);
                    }
                    else
                    {
                        field.Status = SchemaNodeStatus.ApplicationFieldWrongFunc;
                        break;
                    }

                    // Checks the call Arguments
                    if (!string.IsNullOrWhiteSpace(field.Arg))
                    {
                        AppFieldType? pushSource = GetField(field.Arg);
                        if (pushSource is not { SchemaType: ArrayType { ElementSchemaType: not null, Primary: { Length: > 0}} array } ||
                            funcNode.Args[0].SchemaType != null && funcNode.Args[0].SchemaType is not GenericType && 
                            !array.ElementSchemaType.CanBeUseAs(funcNode.Args[0].SchemaType!))
                        {
                            field.Status = SchemaNodeStatus.ApplicationFieldWrongFuncField;
                        }
                        else
                        {
                            // Register to observers
                            pushSource.AddObserver(field);
                            field.PushSource = pushSource;
                    
                            // Compile with data push compile context
                            funcNode.ClearRuntimeFuncCache<DataPushCompileContext>(); // must reset the field reference
                            DataPushCompileContext compileContext = new DataPushCompileContext(context, funcNode);
                            try
                            {
                                FunctionTypeSchema pushSchema = await compileContext.VisitFunctionType();
                                DataPushThirdFieldInfo[] pushField = compileContext.ThirdFields;
                                if (pushField.Length > 0)
                                {
                                    field.ThirdPushFields = compileContext.ThirdFields;
                                    foreach (DataPushThirdFieldInfo push in pushField)
                                        GetField(push.Field)?.AddObserver(field);
                                }
                                field.PushFuncSchema = pushSchema;
                            }
                            catch(FunctionVisitException fv)
                            {
                                field.Status = fv.Status;
                            }
                            catch(Exception ex)
                            {
                                context.LogError(ex,$"AppType.LoadAsync: push function compile error for app {Name} field {field.Name}");
                                field.Status = SchemaNodeStatus.ApplicationPushDataWrongFunc;
                            }
                        }
                    }
                }

                // valid source
                if (!string.IsNullOrWhiteSpace(field.SourceApp) && !string.IsNullOrWhiteSpace(field.SourceField))
                {
                    AppType? sourceApp = await context.GetAppTypeAsync(field.SourceApp);
                    if (sourceApp?.GetField(field.SourceField) == null)
                    {
                        field.Status = SchemaNodeStatus.ApplicationFieldWrongRef;
                    }
                    else
                    {
                        useRef = true;
                        field.SourceAppType = sourceApp;
                    }
                }
                
                if (field.EnableDynamicTable)
                    requireDb = true;
                
                // valid the auths
                if (field.Auths != null)
                {
                    foreach (PolicyItem item in field.Auths)
                    {
                        FunctionType? funcType = !string.IsNullOrEmpty(item.Evaluator)
                            ? await context.GetSchemaTypeAsync(item.Evaluator) as FunctionType
                            : null;
                        if (funcType != null)
                        {
                            item.Function = funcType;
                        }
                        else
                        {
                            field.Status = SchemaNodeStatus.ApplicationFieldDataAuthWrongFunc;
                        }
                    }
                }

                // valid the row policy
                if (field.RowAuths != null)
                {
                    foreach(RowPolicyItem row in field.RowAuths)
                    {
                        // valid evaluator
                        if (!string.IsNullOrEmpty(row.Evaluator))
                        {
                            if (await context.GetSchemaTypeAsync(row.Evaluator) is FunctionType funcType)
                            {
                                row.EvaluatorFunc = funcType;
                            }
                            else
                            {
                                field.Status = SchemaNodeStatus.ApplicationFieldDataAuthWrongFunc;
                            }
                        }
                        // valid filter
                        if (!string.IsNullOrEmpty(row.Filter))
                        {
                            if (await context.GetSchemaTypeAsync(row.Filter) is FunctionType funcType)
                            {
                                row.FilterFunc = funcType;
                            }
                            else
                            {
                                field.Status = SchemaNodeStatus.ApplicationFieldDataAuthWrongFunc;
                            }
                        }
                    }
                }

                // valid the column policy | filter
                StructType? structType = field.SchemaType as StructType
                    ?? (field.SchemaType is ArrayType { ElementSchemaType: StructType st } ? st : null);
                if (structType != null)
                {
                    if (field.ColAuths != null)
                    {
                        foreach(ColPolicyItem colPolicy in field.ColAuths)
                        {
                            StructFieldConfig? structField = structType.GetField(colPolicy.Name);
                            if (structField == null)
                            {
                                field.Status = SchemaNodeStatus.ApplicationFieldDataAuthWrongField;
                                continue;
                            }
                            List<FunctionType> funcs = [];
                            foreach (string item in colPolicy.Evaluators)
                            {
                                FunctionType? funcType = !string.IsNullOrEmpty(item)
                                    ? await context.GetSchemaTypeAsync(item) as FunctionType
                                    : null;
                                if (funcType != null)
                                {
                                    funcs.Add(funcType);
                                }
                                else
                                {
                                    field.Status = SchemaNodeStatus.ApplicationFieldDataAuthWrongFunc;
                                }
                            }
                            colPolicy.Functions = funcs.ToArray();
                        }
                    }

                    if (field.Filters is { Length: > 0 })
                    {
                        foreach (FieldFilter filter in field.Filters)
                        {
                            if (filter.Mode == FieldFilterMode.Filter)
                            {
                                if (await context.GetSchemaTypeAsync(filter.Filter) is not FunctionType funcType ||
                                    funcType.Args.Length < 2 ||
                                    funcType.Args[0].SchemaType == null ||
                                    !funcType.Args[0].SchemaType!.CanBeUseAs(structType))
                                {
                                    field.Status = SchemaNodeStatus.ApplicationFieldDataWrongFilter;
                                    break;
                                }
                            }
                            else
                            {
                                if (structType.GetField(filter.Filter) == null)
                                {
                                    field.Status = SchemaNodeStatus.ApplicationFieldDataWrongFilter;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            // Check the relations
            if (schema.Relations is { Length: > 0 })
            {
                Relations = schema.Relations.Select(r => new AppRelationSchema
                {
                    AppField = r.Field.Split(".", 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty,
                    DataField = r.Field.Contains('.') ? r.Field.Split(".", 2, StringSplitOptions.RemoveEmptyEntries)[1] : string.Empty,
                    Type = r.Type,
                    Func = r.Func,
                    Args = r.Args.Select(a => new AppArgSchema
                    {
                        AppField = a.Name?.Split(".", 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty,
                        DataField = a.Name != null && a.Name.Contains(".") ? a.Name.Split(".", 2, StringSplitOptions.RemoveEmptyEntries)[1] : string.Empty,
                        Value = a.Value,
                    }).ToArray(),
                }).ToList();

                foreach (AppRelationSchema relation in Relations)
                {
                    AppFieldType? field = Fields?.FirstOrDefault(f => f.Name.Equals(relation.AppField, StringComparison.OrdinalIgnoreCase));
                    if (field == null) {
                        relation.Status = SchemaNodeStatus.ApplicationRelationWrongTarget;
                        continue;
                    }
                    relation.FieldNode = field;

                    if (string.IsNullOrWhiteSpace(relation.Func))
                    {
                        relation.Status = SchemaNodeStatus.ApplicationRelationWrongFunc;
                    }
                    else
                    {
                        AnySchemaType? relationFunc = await context.GetSchemaTypeAsync(relation.Func);
                        if (relationFunc is FunctionType funcNode)
                        {
                            funcNode.AddRef(field);
                            relation.FuncNode = funcNode;
                        }
                        else
                        {
                            field.Status = SchemaNodeStatus.StructRelationshipWrongFunc;
                        }
                    }
                }
            }
            
            // Use ref
            if (requireDb && useRef) {
                string refType = typeof(List<AppRef>).GetSchemaType()!;

                RefField = new AppFieldType
                {
                    App = Name,
                    Name = APP_FIELD_REF_NAME,
                    Type = refType,
                    SchemaType = await context.GetSchemaTypeAsync(refType)
                };
            }
            else
            {
                RefField = null;
            }
        }

        // load data auths
        if (Auths != null)
        {
            foreach (var item in Auths)
            {
                AnySchemaType? node = !string.IsNullOrEmpty(item.Evaluator)
                    ? await context.GetSchemaTypeAsync(item.Evaluator)
                    : null;
                if (node is FunctionType funcNode)
                {
                    item.Function = funcNode;
                    item.Status = SchemaNodeStatus.Ready;
                }
                else
                {
                    item.Status = SchemaNodeStatus.PolicyWrongFunc;
                }
            }
        }
        
        // load workflows
        List<AppWorkflowType>? oldWorkflows = Workflows;
        Workflows = schema.Workflows?.Select(w =>
        {
            // if the workflow is activated, keep the old instance to avoid breaking the running workflow, otherwise create a new instance
            AppWorkflowType wft = oldWorkflows?.FirstOrDefault(o => o.Name.Equals(w.Name, StringComparison.OrdinalIgnoreCase)) is { Activated: true } old
                ? old : w;
            wft.Application = this;
            return wft;
        }).ToList();
        foreach(var wf in Workflows ?? [])
        {
            if (Injection.WorkflowTypes != null)
                Injection.WorkflowTypes.Add(wf);
            else
                await wf.LoadAsync(context);
        }
        
        // preload sub applications
        if (preLoad && Apps is { Length: > 0 })
        {
            // Load all the sub application list
            foreach (string name in Apps.Select(p => p.Name))
                await context.GetAppTypeAsync(name, preload: true);
        }
    }
    
    /// <summary>
    /// Release usages
    /// </summary>
    public void Release()
    {
        // Release the old field relationships
        Fields?.ForEach(p =>
        {
            p.SchemaType?.RemoveRef(p);
            p.FuncNode?.RemoveRef(p);
        });
        Relations?.ForEach(r =>
        {
            if (r.FieldNode != null)
                r.FuncNode?.RemoveRef(r.FieldNode);
        });
    }

    /// <summary>
    /// Gets the authentication policies with the scope
    /// </summary>
    public IEnumerable<PolicyItem> GetAuthPolicies(PolicyScope scope)
    {
        // use system for root
        if (RootApp == null)
        {
            if (SubAppList?[NS_SYSTEM] is { } system)
            {
                foreach (var item in system.GetAuthPolicies(scope))
                    yield return item;
            }
        }
        // system won't inherit auth from root app
        else if (!Name.Equals(NS_SYSTEM))
        {
            foreach (var item in RootApp.GetAuthPolicies(scope))
                yield return item;
        }

        if (Auth != null)
        {
            foreach (var item in Auth.Items.Where(p => p.Scope == scope))
                yield return item;
        }

        if (Auths != null)
        {
            foreach (var item in Auths.Where(p => p.Scope == scope))
                yield return item;
        }
    }
    
    /// <summary>
    /// Gets the app field by name
    /// </summary>
    public AppFieldType? GetField(string? name)
    {
        return !string.IsNullOrWhiteSpace(name)
            ? Fields?.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            : null;
    }

    /// <summary>
    /// Gets the workflow by name
    /// </summary>
    public AppWorkflowType? GetWorkflow(string? name)
    {
        return !string.IsNullOrWhiteSpace(name)
            ? Workflows?.FirstOrDefault(w => w.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            : null;
    }
    
    /// <summary>
    /// Gets all node schemas used by the application
    /// </summary>
    /// <returns></returns>
    public async Task<NodeSchema[]> GetNodeSchemas(SchemaContext ctx, NodeSchema? root = null, HashSet<string>? types = null, bool includeUsedBy = false, CancellationToken? cancellationToken = null)
    {
        if (Fields == null || Fields.Count == 0)
            return [];

        types ??= [];
        root ??= new NodeSchema
        {
            Name = "",
            Type = SchemaType.Namespace,
            Schemas = []
        };

        foreach (AppFieldType fieldNode in Fields)
        {
            cancellationToken?.ThrowIfCancellationRequested();
            
            if (fieldNode.SchemaType != null)
                await fieldNode.SchemaType.GetNodeSchemas(ctx, root, types, includeUsedBy, cancellationToken);
            if (fieldNode.FuncNode != null)
                await fieldNode.FuncNode.GetNodeSchemas(ctx, root, types, includeUsedBy, cancellationToken);
            if (fieldNode.Filters is { Length: > 0 })
            {
                foreach (FieldFilter filter in fieldNode.Filters.Where(f => f.Mode == FieldFilterMode.Filter))
                {
                    AnySchemaType? filterType = await ctx.GetSchemaTypeAsync(filter.Filter);
                    if (filterType != null)
                        await filterType.GetNodeSchemas(ctx, root, types, includeUsedBy, cancellationToken);
                }
            }
        }

        if (Relations is { Count: > 0 })
        {
            foreach (AppRelationSchema relation in Relations)
            {
                cancellationToken?.ThrowIfCancellationRequested();
                
                if (relation.FuncNode != null)
                    await relation.FuncNode.GetNodeSchemas(ctx, root, types, includeUsedBy, cancellationToken);
            }
        }

        return root.Schemas!;
    }

    #endregion
} 

