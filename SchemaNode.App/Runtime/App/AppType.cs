using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Collections.Concurrent;
using SchemaNode.Node;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory application schema representation
/// </summary>
public sealed class AppType : IValueTypeAccess
{
    #region Fields
    
    // The application schema
    private AppSchema? _schema;
    
    // The sub application node
    private ConcurrentDictionary<string, AppType>? _subApps;
    private ConcurrentDictionary<string, AppType>.AlternateLookup<ReadOnlySpan<char>>? _appLookup;
    private ConcurrentDictionary<string, AppSchema>? _schemas;
    private ConcurrentDictionary<string, AppSchema>.AlternateLookup<ReadOnlySpan<char>>? _schemaLookup;

    // The application field nodes
    private List<AppFieldType>? _fields;
    
    // The application workflows
    private List<AppWorkflowType>? _workflows;

    //  The relations
    private List<RelationType>? _relations;
    
    // properties
    private IProperty[]? _props;
    private NodeType[]? _refTypes;
    
    #endregion
    
    #region Properties
    
    /// <summary>
    /// The application name
    /// </summary>
    public string Name => _schema?.FullName ?? string.Empty;

    /// <summary>
    /// The target policies, can only be changeable when no app & no fields or in debug mode
    /// </summary>
    public AppScopePolicy? ScopePolicy => _schema?.ScopePolicy;

    /// <summary>
    /// The target scope type, default to business target if no scope policy defined
    /// </summary>
    public AppScopeType ScopeType => ScopePolicy?.Type ?? AppScopeType.BusinessTarget;
    
    /// <summary>
    /// The root application
    /// </summary>
    public AppType? Container { get; init; }

    /// <summary>
    /// The sub applications
    /// </summary>
    public AppSchema[]? Apps { get; internal set; }

    #endregion

    #region State

    /// <summary>
    /// The schema node error code
    /// </summary>
    public string? Error { get; private set; }
    
    /// <summary>
    /// The application is used
    /// </summary>
    public bool IsUsed => _fields is { Count: > 0 } || Apps is { Length: > 0 };
    
    /// <summary>
    /// Already loaded
    /// </summary>
    internal bool Loaded { get; set; }

    #endregion

    #region Container Methods

    /// <summary>
    /// Gets the sub application type
    /// </summary>
    public AppType? GetAppType(ReadOnlySpan<char> name)
    {
        if (_subApps == null) return null;
        _appLookup ??= _subApps.GetAlternateLookup<ReadOnlySpan<char>>();
        return _appLookup.Value.TryGetValue(name, out AppType? app) ? app : null;
    }

    /// <summary>
    /// Saves the app type by segment name
    /// </summary>
    internal void SaveAppType(ReadOnlySpan<char> name, AppType app)
    {
        _subApps ??= [];
        _subApps[name.ToString()] = app;
    }

    /// <summary>
    /// Gets the app schema
    /// </summary>
    internal AppSchema? GetAppSchema(ReadOnlySpan<char> name)
    {
        if (_schemas == null) return null;
        _schemaLookup ??= _schemas.GetAlternateLookup<ReadOnlySpan<char>>();
        return _schemaLookup.Value.TryGetValue(name, out AppSchema? schema) ? schema : null;
    }

    /// <summary>
    /// Saves the app schema
    /// </summary>
    internal void SaveAppSchema(AppSchema schema)
    {
        _schemas ??= [];
        _schemas[schema.Name] = schema;
    }
    
    
    #endregion
    
    #region Methods

    /// <summary>
    /// Load the data
    /// </summary>
    internal async Task LoadAsync(SchemaContext context, AppSchema schema)
    {
        // Release old usages
        Release();
        _relations = null;

        // data
        _schema  = schema;
        _props = schema.GetProperties(context.Runtime.GetSchemaKindProperties(SCHEMA_KIND_APP)).ToArray();

        (_refTypes, Error) = await schema.LoadPropertiesAsync(context, _props);

        // Load the application fields
        _fields = schema.Fields?.Select(f => new AppFieldType(this, f)).ToList() ?? [];

        foreach (AppFieldType appFieldType in _fields)
        {
            await appFieldType.LoadAsync(context);
            Error ??= appFieldType.Error;
        }
        
        // Reload Apps
        List<AppType>? reloadApps = null;

        // Check the relations
        if (schema.GetProperty<Relations>()?.Value is { Length: > 0 } relations)
        {
            foreach (RelationSchema relation in relations)
            {
                // Gets the target type
                ValueType? currentType = GetAccessValueType(relation.Target);
                if (currentType == null) continue;
                
                // Only work for constraint properties
                Type? propType = context.Runtime.GetSchemaKindPropertyByName(currentType.Kind, relation.Property);
                if (propType == null || !typeof(IConstraintProperty).IsAssignableFrom(propType)) continue;
                
                var relationType = await relation.LoadAsync(context, this);
                Error ??= relationType.Error;

                _relations ??= [];
                _relations.Add(relationType);
            }
        }
        
        // load workflows
        List<AppWorkflowType>? oldWorkflows = _workflows;
        _workflows = schema.Workflows?.Select(w =>
        {
            // if the workflow is activated, keep the old instance to avoid breaking the running workflow, otherwise create a new instance
            AppWorkflowType wft = oldWorkflows?.FirstOrDefault(o => o.Name.Equals(w.Name, StringComparison.OrdinalIgnoreCase)) is { Activated: true } old
                ? old : w;
            wft.Application = this;
            wft.Properties = wft.Extensions != null ? PropertyType.GetProperties<IProperty>(context, Enum.SchemaType.AppWorkflow, wft.Extensions)?.ToArray() : null;
            return wft;
        }).ToList();
        foreach(var wf in _workflows ?? [])
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
        _fields?.ForEach(p =>
        {
            p.SchemaType?.RemoveRef(p);
            p.FuncNode?.RemoveRef(p);
        });
        Relations?.ForEach(r =>
        {
            if (r.FieldNode != null)
                r.FuncNode?.RemoveRef(r.FieldNode);
        });
        _workflows?.ForEach(w => w.Release());
    }

    /// <summary>
    /// Gets the property with given type
    /// </summary>
    public T? GetProperty<T>() where T : class, IProperty => _props?.OfType<T>().FirstOrDefault();

    /// <summary>
    /// Gets the constraints
    /// </summary>
    public IEnumerable<T> GetProperties<T>() => _props?.OfType<T>() ?? [];
    
    /// <summary>
    /// Gets the property by property name
    /// </summary>
    public IProperty? GetProperty(string propertyName) => _props?.FirstOrDefault(p => p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Gets the app field by name
    /// </summary>
    public AppFieldType? GetField(string name) => _fields?.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Gets the workflow by name
    /// </summary>
    public AppWorkflowType? GetWorkflow(string name) => _workflows?.FirstOrDefault(w => w.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    
    /// <summary>
    /// Gets relations
    /// </summary>
    public IEnumerable<RelationType> GetRelations() => _relations?.AsEnumerable() ?? [];
    
    /// <summary>
    /// Gets relations for the given field name
    /// </summary>
    public IEnumerable<RelationType> GetRelations(string fieldName)
        => _relations?.Where(r => fieldName.Equals(r.Target, StringComparison.OrdinalIgnoreCase)) ?? [];
    
    /// <summary>
    /// Gets the access value type
    /// </summary>
    public ValueType? GetAccessValueType(string path)
    {
        if (_fields == null || _fields.Count == 0) return null;
        ReadOnlySpan<char> remain = null;
        int index = path.IndexOf('.');
        if (index > 0)
        {
            remain = path.AsSpan()[(index + 1)..];
            path = path[..index];
        }
        foreach (AppFieldType field in _fields)
        {
            if (path.Equals(field.Name, StringComparison.OrdinalIgnoreCase))
                return remain.IsEmpty ? field.ValueType : field.ValueType?.GetAccessValueType(remain.ToString());
        }
        return null;
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

        if (_fields is { Count: > 0 })
        {
            foreach (AppFieldType fieldNode in _fields)
            {
                cancellationToken?.ThrowIfCancellationRequested();

                if (fieldNode.ValueType != null)
                    await fieldNode.ValueType.GetNodeSchemas(ctx, root, types, includeUsedBy, cancellationToken);

                if (fieldNode.PushFunc != null)
                    await fieldNode.PushFunc.GetNodeSchemas(ctx, root, types, includeUsedBy, cancellationToken);

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
        if (_workflows is { Count: > 0 })
        {
            foreach (AppWorkflowType workflow in _workflows)
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
    internal IEnumerable<(string item, ValueType type, bool isTarget)> GetScopeContextItems()
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
                    mapType = field?.SchemaType;
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
    public IEnumerable<(string item, DataNode? value, bool isTarget)> GetScopeContextItems(SchemaContext ctx)
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
                    mapType = field?.SchemaType;
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

