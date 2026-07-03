using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Property;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Collections.Concurrent;
using SchemaNode.Node;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;
using SchemaNode.Property.App;
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
    public AppScopePolicy? ScopePolicy { get; private set; }

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
        // system app schema without other provider doesn't need reload
        if (_schemas != null && _schemas.TryGetValue(schema.Name, out AppSchema? exist) && (exist.LoadState & SchemaLoadState.System) > 0 && schema.Provider == null) return;
        
        // record and mark unload
        _schemas ??= [];
        _schemas[schema.Name] = schema;
        if (_subApps != null && _subApps.TryGetValue(schema.Name, out AppType? app)) app.Loaded = false;
    }

    internal void RemoveAppSchema(ReadOnlySpan<char> name)
    {
        _schemas?.TryRemove(name.ToString(), out _);
    }
    
    internal IEnumerable<AppSchema> GetSubAppSchemas() => _schemas?.Values ?? [];
    
    public IEnumerable<AppType> GetSubApps() => _subApps?.Values ?? [];
    
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
        ScopePolicy = GetProperty<ScopePolicy>()?.Value;

        // Load the application fields
        _fields = schema.Fields?.Select(f => new AppFieldType(this, f)).ToList() ?? [];

        foreach (AppFieldType appFieldType in _fields)
        {
            await appFieldType.LoadAsync(context);
            Error ??= appFieldType.Error;
        }
        
        // Check the relations
        if (schema.GetProperty<Relations>()?.Value is { Length: > 0 } relations)
        {
            foreach (RelationSchema relation in relations)
            {
                // Gets the target type
                ValueType? currentType = GetAccessValueType(relation.Target);
                if (currentType == null) continue;
                
                // Gets the property type
                PropertyType? prop = await context.GetNodeTypeAsync<PropertyType>(relation.Property);
                if (prop == null) continue;
                
                // Only work for constraint properties
                Type? propType = context.Runtime.GetSchemaKindPropertyByName(currentType.Kind, prop.Property);
                if (propType == null) continue;
                
                var relationType = await relation.LoadAsync(context, this);
                Error ??= relationType.Error;

                _relations ??= [];
                _relations.Add(relationType);
            }
        }
        
        // load workflows
        List<AppWorkflowType>? oldWorkflows = _workflows;
        _workflows = [];
        if (schema.Workflows is { Length: > 0 })
        {
            foreach (AppWorkflowSchema w in schema.Workflows)
            {
                _workflows.Add(oldWorkflows?.FirstOrDefault(o => o.Name.Equals(w.Name, StringComparison.OrdinalIgnoreCase)) is { Activated: true } old
                    ? old : new AppWorkflowType(this, w));
            }
        }
        foreach(var wf in _workflows ?? [])
        {
            // Runtime already activated, need to load workflow immediately, otherwise it will be loaded in app activation
            if (context.Runtime.Stage == RuntimeStage.Activated)
                await wf.LoadAsync(context);
            else
                (context.Runtime as SchemaRuntime)?.GetOrAddRuntimeItem<AppWorkflowQueue>()?.Enqueue(wf);
        }

        // Add referenced by for fields, workflows and relations
        foreach (NodeType t in GetReferenceTypes())
            t.AddUsedBy(this);
    }

    /// <summary>
    /// Release usages
    /// </summary>
    public void Release()
    {
        foreach (NodeType t in GetReferenceTypes())
            t.RemoveUsedBy(this);
    }

    public IEnumerable<NodeType> GetReferenceTypes()
    {
        if (_fields != null)
            foreach (AppFieldType f in _fields)
                foreach (NodeType t in f.GetReferenceTypes())
                    yield return t;

        if (_workflows != null)
            foreach (AppWorkflowType w in _workflows)
                foreach (NodeType t in w.GetReferenceTypes())
                    yield return t;
        
        
        if (_relations != null)
            foreach (RelationType r in _relations)
                foreach (NodeType t in r.GetReferenceTypes())
                    yield return t;

        if (_refTypes != null)
            foreach (NodeType t in _refTypes)
                yield return t;
    }

    /// <summary>
    /// Gets the schema of the application
    /// </summary>
    public AppSchema GetSchema()
    {
        if (_schema == null) return new  AppSchema();
        AppSchema schema = new AppSchema
        {
            Name = Name,
            Container = _schema.Container,
            Fields = _fields?.Select(f => f.GetSchema()).ToArray(),
            Workflows = _workflows?.Select(w => w.GetSchema()).ToArray(),
        };
        schema.CombineProperties(_schema);
        return schema;
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
    /// Gets the app fields
    /// </summary>
    public IEnumerable<AppFieldType> GetFields() => _fields ?? [];
    
    /// <summary>
    /// Gets the app field by name
    /// </summary>
    public AppFieldType? GetField(string name) => _fields?.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    
    /// <summary>
    /// Gets the app workflows
    /// </summary>
    public IEnumerable<AppWorkflowType>  GetWorkflows() => _workflows ?? [];

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
            Kind = SCHEMA_KIND_NAMESPACE,
            Schemas = []
        };

        foreach (NodeType t in GetReferenceTypes())
        {
            cancellationToken?.ThrowIfCancellationRequested();
            await t.GetNodeSchemas(ctx, root, types, false, includeUsedBy, cancellationToken);
        }
        return root.Schemas!;
    }

    /// <summary>
    /// Gets the scope context items for the application, which will be used for policy evaluation and data push
    /// </summary>
    internal IEnumerable<(string item, ValueType type, bool isTarget)> GetScopeContextItemTypes(SchemaContext ctx)
    {
        if (ScopePolicy?.Type == AppScopeType.SystemLevel)
            yield break;

        bool tarCovered = false;
        StructType contextType = ctx.System.Context;
        if (ScopePolicy is { ContextMaps.Length: > 0 })
        {
            foreach (var map in ScopePolicy.ContextMaps)
            {
                ValueType? mapType = contextType;
                string last = map.ContextItem;
                bool isTarget = map.ContextItem.Equals(TargetAccess, StringComparison.OrdinalIgnoreCase);
                if (isTarget) tarCovered = true;

                foreach (string path in map.ContextItem.Split('.', StringSplitOptions.RemoveEmptyEntries))
                {
                    var field = mapType is StructType st ? st.GetField(path) : null;
                    mapType = field?.Type;
                    last = field?.Name ?? string.Empty;
                }

                if (mapType == null)
                    throw new Exception($"Invalid context item {map.ContextItem} in app {Name} scope policy");
                
                yield return (!string.IsNullOrWhiteSpace(map.MapKey) ? map.MapKey : $"_{last}", mapType, isTarget);
            }
        }
        if (!tarCovered)
            yield return (DefaultTarget, ctx.System.String, true);
    }
    
    /// <summary>
    /// Gets the scope context items for the application, which will be used for policy evaluation and data push
    /// </summary>
    public IEnumerable<(string item, DataNode? value, bool isTarget)> GetScopeContextItems(SchemaContext ctx)
    {
        if (ScopePolicy?.Type == AppScopeType.SystemLevel)
            yield break;

        bool tarCovered = false;
        ValueType contextType = ctx.System.Context;
        if (ScopePolicy is { ContextMaps.Length: > 0 })
        {
            foreach (var map in ScopePolicy.ContextMaps)
            {
                var mapType = contextType;
                string last = map.ContextItem;
                bool isTarget = map.ContextItem.Equals(TargetAccess, StringComparison.OrdinalIgnoreCase);
                if (isTarget) tarCovered = true;

                foreach (string path in map.ContextItem.Split('.', StringSplitOptions.RemoveEmptyEntries))
                {
                    var field = mapType is StructType st ? st.GetField(path) : null;
                    mapType = field?.Type;
                    last = field?.Name ?? string.Empty;
                }

                if (mapType == null)
                    throw new Exception($"Invalid context item {map.ContextItem} in app {Name} scope policy");
                
                yield return (!string.IsNullOrWhiteSpace(map.MapKey) ? map.MapKey : $"_{last}", ctx.GetContextItem(map.ContextItem), isTarget);
            }
        }
        if (!tarCovered)
            yield return (DefaultTarget, ctx.GetContextItem(TargetAccess), true);
    }

    private static readonly string DefaultTarget = $"_{nameof(Access.Target).ToCamelCase()}";
    const string TargetAccess = $"{nameof(Access)}.{nameof(Access.Target)}";

    #endregion
}

internal class AppWorkflowQueue : ConcurrentQueue<AppWorkflowType>;