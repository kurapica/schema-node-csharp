using Microsoft.AspNetCore.Http.Features;
using SchemaNode.Components;
using SchemaNode.Components.Property.Presentation;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Collections.Concurrent;
using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory application schema representation
/// </summary>
public sealed class AppType
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
    public LocaleString? Desc => Properties?.FirstOrDefault(p => p is DescProperty) is DescProperty desc ? desc.Value : null;

    /// <summary>
    /// The target policies, can only be changeable when no app & no fields or in debug mode
    /// </summary>
    public AppScopePolicy? ScopePolicy { get; private set; }

    /// <summary>
    /// The target scope type, default to business target if no scope policy defined
    /// </summary>
    public AppScopeType ScopeType => ScopePolicy?.Type ?? AppScopeType.BusinessTarget;
    
    /// <summary>
    /// The authentication policy type
    /// </summary>
    public PolicyType? Auth { get; private set; }
    
    /// <summary>
    /// The data authentication policy type
    /// </summary>
    public PolicyItem[]? Auths { get; private set; }

    /// <summary>
    /// The application field relations
    /// </summary>
    public List<AppRelationSchema>? Relations { get; private set; }

    /// <summary>
    /// The sub applications
    /// </summary>
    public AppSchema[]? Apps { get; internal set; }

    /// <summary>
    /// The properties
    /// </summary>
    public IProperty[]? Properties { get; private set; }

    /// <summary>
    /// The additional data
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; internal set; }
    
    /// <summary>
    /// The root application
    /// </summary>
    public AppType? RootApp { get; init; }

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
        ScopePolicy = schema.ScopePolicy;
        Auth = !string.IsNullOrEmpty(schema.Auth)
            ? await context.GetSchemaTypeAsync(schema.Auth) as PolicyType
            : null;
        Auths = schema.Auths;
        Apps = schema.Apps;
        Additional = schema.Additional;
        Properties = Additional != null ? PropertyType.GetProperties<IProperty>(context, Enum.SchemaType.App, Additional)?.ToArray() : null;

        // Load the application fields
        Fields = schema.Fields?.Select(p => (AppFieldType)p).ToList();
        Relations = null;
        
        // Reload Apps
        List<AppType>? reloadApps = null;

        if (Fields is { Count: > 0 })
        {
            // load field type first to avoid circular reference
            foreach (AppFieldType field in Fields)
            {
                field.App = Name;
                field.Application = this;
                field.Status = null;
                field.Properties = field.Additional != null ? PropertyType.GetProperties<IProperty>(context, Enum.SchemaType.AppField, field.Additional)?.ToArray() : null;

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
                    foreach(RowPolicy row in field.RowAuths)
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
                        foreach(ColPolicy colPolicy in field.ColAuths)
                        {
                            StructFieldSchema? structField = structType.GetField(colPolicy.Name);
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

                // valid the foreign key reference
                if (field.Foreigns is { Length: > 0})
                {
                    foreach (Foreign foreign in field.Foreigns)
                    {
                        if (string.IsNullOrWhiteSpace(foreign.Field) ||
                            string.IsNullOrWhiteSpace(foreign.App) ||
                            await context.GetAppTypeAsync(foreign.App) is not AppType refApp ||
                            refApp.ScopeType == AppScopeType.SystemLevel ||
                            structType == null || 
                            structType.GetField(foreign.Field) == null)
                        {
                            field.Status = SchemaNodeStatus.ApplicationFieldWrongRef;
                            break;
                        }
                        reloadApps ??= [];
                        reloadApps.Add(refApp);
                    }
                }

                // Check source app & field as view
                if (!string.IsNullOrWhiteSpace(field.View?.App) || !string.IsNullOrWhiteSpace(field.View?.Field))
                {
                    if (structType == null || string.IsNullOrWhiteSpace(field.View?.App) || string.IsNullOrWhiteSpace(field.View?.Field) ||
                        await context.GetAppTypeAsync(field.View.App) is not AppType sourceApp ||
                        sourceApp.ScopeType == AppScopeType.SystemLevel ||
                        sourceApp.GetField(field.View.Field) is not AppFieldType sourceField ||
                        sourceField.Foreigns == null || sourceField.Foreigns.Length == 0 || 
                        sourceField.Foreigns.All(f => !f.App.Equals(Name, StringComparison.OrdinalIgnoreCase)) ||
                        !string.IsNullOrWhiteSpace(field.View.Map) && structType.GetField(field.View.Map) == null)
                    {
                        field.Status = SchemaNodeStatus.ApplicationFieldWrongRef;
                    }
                    else
                    {
                        field.View.AppType = sourceApp;
                        AnySchemaType? sourceFieldType = sourceField.SchemaType;
                        if (sourceFieldType is ArrayType arrType)
                            sourceFieldType = arrType.ElementSchemaType;

                        if (sourceFieldType is not StructType sourceStruct)
                        {
                            field.Status = SchemaNodeStatus.ApplicationFieldWrongRef;
                        }
                        else
                        {
                            // Check fields
                            foreach (var f in structType.Fields)
                            {
                                if (f.SchemeType == null)
                                {
                                    field.Status = SchemaNodeStatus.ApplicationFieldWrongRef;
                                    break;
                                }

                                if (f.Name.Equals(field.View.Map, StringComparison.OrdinalIgnoreCase))
                                    continue;

                                // Match the source field
                                if (f.DisplayOnly == true)
                                {
                                    // Can be generated by other fields, or from the other field of the source app
                                    StructRelationSchema? relation = structType.Relations?.FirstOrDefault(r =>
                                        r.Field.Equals(f.Name, StringComparison.OrdinalIgnoreCase) &&
                                        r.Property.Equals(PROPERTY_DEFAULT, StringComparison.OrdinalIgnoreCase));

                                    if (relation == null || DynamicTableSchema.IsReferenceFunc(relation.Func) &&
                                        !sourceApp.Name.Equals(relation.Args.FirstOrDefault()?.Value?.ToValue<string>(), StringComparison.OrdinalIgnoreCase))
                                    {
                                        field.Status = SchemaNodeStatus.ApplicationFieldWrongRef;
                                        break;
                                    }
                                }
                                else if (sourceStruct.GetField(f.Name) is { SchemeType: { } } sourceFieldMatch)
                                {
                                    if (!sourceFieldMatch.SchemeType.CanBeUseAs(f.SchemeType))
                                    {
                                        field.Status = SchemaNodeStatus.ApplicationFieldWrongRef;
                                        break;
                                    }
                                }
                                else
                                {
                                    field.Status = SchemaNodeStatus.ApplicationFieldWrongRef;
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
                    Property = r.Property,
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
            wft.Properties = wft.Additional != null ? PropertyType.GetProperties<IProperty>(context, Enum.SchemaType.AppWorkflow, wft.Additional)?.ToArray() : null;
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

        // reload the foreign if changes
        if (!preLoad && reloadApps is {  Count: > 0 })
        {
            // reload the reference applications to update the observers
            foreach (AppType app in reloadApps)
                await context.GetAppTypeAsync(app.Name, reload: true);
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
            if (SubAppList?.TryGetValue(NS_SYSTEM, out AppType? system) == true)
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
        types ??= [];
        root ??= new NodeSchema
        {
            Name = "",
            Type = Enum.SchemaType.Namespace,
            Schemas = []
        };

        // App-level auth policy type
        if (Auth != null)
            await Auth.GetNodeSchemas(ctx, root, types, includeUsedBy, cancellationToken);

        // App-level data auth functions
        if (Auths != null)
        {
            foreach (PolicyItem item in Auths)
            {
                cancellationToken?.ThrowIfCancellationRequested();
                if (item.Function != null)
                    await item.Function.GetNodeSchemas(ctx, root, types, includeUsedBy, cancellationToken);
            }
        }

        if (Fields is { Count: > 0 })
        {
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

                // Field-level data auth functions
                if (fieldNode.Auths != null)
                {
                    foreach (PolicyItem item in fieldNode.Auths)
                    {
                        if (item.Function != null)
                            await item.Function.GetNodeSchemas(ctx, root, types, includeUsedBy, cancellationToken);
                    }
                }

                // Row policy functions
                if (fieldNode.RowAuths != null)
                {
                    foreach (RowPolicy row in fieldNode.RowAuths)
                    {
                        if (row.EvaluatorFunc != null)
                            await row.EvaluatorFunc.GetNodeSchemas(ctx, root, types, includeUsedBy, cancellationToken);
                        if (row.FilterFunc != null)
                            await row.FilterFunc.GetNodeSchemas(ctx, root, types, includeUsedBy, cancellationToken);
                    }
                }

                // Column policy functions
                if (fieldNode.ColAuths != null)
                {
                    foreach (ColPolicy colPolicy in fieldNode.ColAuths)
                    {
                        foreach (FunctionType func in colPolicy.Functions)
                            await func.GetNodeSchemas(ctx, root, types, includeUsedBy, cancellationToken);
                    }
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

        // Workflow node schema types
        if (Workflows is { Count: > 0 })
        {
            foreach (AppWorkflowType workflow in Workflows)
            {
                cancellationToken?.ThrowIfCancellationRequested();

                // Workflow-level auth functions
                if (workflow.Auths != null)
                {
                    foreach (PolicyItem item in workflow.Auths)
                    {
                        if (item.Function != null)
                            await item.Function.GetNodeSchemas(ctx, root, types, includeUsedBy, cancellationToken);
                    }
                }

                // Workflow node referenced types
                foreach (AppWorkflowNodeSchema node in workflow.Nodes)
                {
                    cancellationToken?.ThrowIfCancellationRequested();

                    if (!string.IsNullOrWhiteSpace(node.Type))
                    {
                        AnySchemaType? wfType = await ctx.GetSchemaTypeAsync(node.Type);
                        if (wfType != null)
                            await wfType.GetNodeSchemas(ctx, root, types, includeUsedBy, cancellationToken);
                    }

                    if (!string.IsNullOrWhiteSpace(node.Func))
                    {
                        AnySchemaType? funcType = await ctx.GetSchemaTypeAsync(node.Func);
                        if (funcType != null)
                            await funcType.GetNodeSchemas(ctx, root, types, includeUsedBy, cancellationToken);
                    }

                    if (!string.IsNullOrWhiteSpace(node.Event))
                    {
                        AnySchemaType? eventType = await ctx.GetSchemaTypeAsync(node.Event);
                        if (eventType != null)
                            await eventType.GetNodeSchemas(ctx, root, types, includeUsedBy, cancellationToken);
                    }

                    if (!string.IsNullOrWhiteSpace(node.Payload))
                    {
                        AnySchemaType? payloadType = await ctx.GetSchemaTypeAsync(node.Payload);
                        if (payloadType != null)
                            await payloadType.GetNodeSchemas(ctx, root, types, includeUsedBy, cancellationToken);
                    }
                }
            }
        }

        return root.Schemas!;
    }

    /// <summary>
    /// Gets the scope context items for the application, which will be used for policy evaluation and data push
    /// </summary>
    public IEnumerable<(string item, AnySchemaType type, bool isTarget)> GetScopeContextItems()
    {
        if (ScopePolicy?.Type == AppScopeType.SystemLevel)
            yield break;

        bool tarCovered = false;
        StructType contextType = SchemaContext.SystemContext;
        if (ScopePolicy is { ContextMaps.Length: > 0 })
        {
            foreach (var map in ScopePolicy.ContextMaps)
            {
                AnySchemaType? mapType = contextType;
                string last = map.ContextItem;
                bool isTarget = map.ContextItem.Equals(TargetAccess, StringComparison.OrdinalIgnoreCase);
                if (isTarget) tarCovered = true;

                foreach (string path in map.ContextItem.Split('.', StringSplitOptions.RemoveEmptyEntries))
                {
                    StructFieldSchema? field = mapType is StructType st ? st.GetField(path) : null;
                    mapType = field?.SchemeType;
                    last = field?.Name ?? string.Empty;
                }

                if (mapType == null)
                    throw new Exception($"Invalid context item {map.ContextItem} in app {Name} scope policy");
                
                yield return (!string.IsNullOrWhiteSpace(map.MapKey) ? map.MapKey : $"_{last}", mapType, isTarget);
            }
        }
        if (!tarCovered)
            yield return (DefaultTarget, SchemaContext.SystemString, true);
    }
    
    /// <summary>
    /// Gets the scope context items for the application, which will be used for policy evaluation and data push
    /// </summary>
    public IEnumerable<(string item, AnySchemaNode? value, bool isTarget)> GetScopeContextItems(SchemaContext ctx)
    {
        if (ScopePolicy?.Type == AppScopeType.SystemLevel)
            yield break;

        bool tarCovered = false;
        StructType contextType = SchemaContext.SystemContext;
        if (ScopePolicy is { ContextMaps.Length: > 0 })
        {
            foreach (var map in ScopePolicy.ContextMaps)
            {
                AnySchemaType? mapType = contextType;
                string last = map.ContextItem;
                bool isTarget = map.ContextItem.Equals(TargetAccess, StringComparison.OrdinalIgnoreCase);
                if (isTarget) tarCovered = true;

                foreach (string path in map.ContextItem.Split('.', StringSplitOptions.RemoveEmptyEntries))
                {
                    StructFieldSchema? field = mapType is StructType st ? st.GetField(path) : null;
                    mapType = field?.SchemeType;
                    last = field?.Name ?? string.Empty;
                }

                if (mapType == null)
                    throw new Exception($"Invalid context item {map.ContextItem} in app {Name} scope policy");
                
                yield return (!string.IsNullOrWhiteSpace(map.MapKey) ? map.MapKey : $"_{last}", ctx.GetSchemaContextItem(map.ContextItem), isTarget);
            }
        }
        if (!tarCovered)
            yield return (DefaultTarget, ctx.GetSchemaContextItem(TargetAccess), true);
    }

    static string DefaultTarget = $"_{nameof(Access.Target).ToCamelCase()}";
    const string TargetAccess = $"{nameof(Access)}.{nameof(Access.Target)}";

    #endregion
} 

