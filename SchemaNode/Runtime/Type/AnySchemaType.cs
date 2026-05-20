using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Schema;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory schema representation
/// </summary>
public abstract class AnySchemaType: IDisposable
{
    #region Data

    /// <summary>
    /// The namespace
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The schema display
    /// </summary>
    public LocaleString? Display { get; internal set; }
    
    /// <summary>
    /// The authentication policy type
    /// </summary>
    public PolicyType? Auth { get; set; }
    
    /// <summary>
    /// The namespace that holds the type
    /// </summary>
    public TypeNamespace? Namespace { get; set; }
    
    #endregion
    
    #region Status
        
    /// <summary>
    /// The schema type
    /// </summary>
    public virtual SchemaType Type => SchemaType.Namespace;

    /// <summary>
    /// Is value type
    /// </summary>
    public virtual bool IsValueType => false;

    /// <summary>
    /// The load state
    /// </summary>
    public SchemaLoadState LoadState { get; init; } = SchemaLoadState.Server;

    /// <summary>
    /// The schema node status
    /// </summary>
    public SchemaNodeStatus Status { get; internal set; } = SchemaNodeStatus.Ready;
        
    /// <summary>
    /// The scheme provider used to load the node
    /// </summary>
    public Type? SchemaProvider { get; internal set; }
    
    /// <summary>
    /// Whether the node is used
    /// </summary>
    public virtual bool IsUsed => UsedBy is { IsEmpty: false } || UsedByApp is { IsEmpty: false } || UsedByWorkflow is { IsEmpty: false };
    
    /// <summary>
    /// The type is loaded
    /// </summary>
    internal bool Loaded { get; set; }

    #endregion

    #region Properties

    /// <summary>
    /// The extensions
    /// </summary>
    protected Dictionary<string, JsonElement>? Extensions { get; set; }

    /// <summary>
    /// The properties
    /// </summary>
    protected IProperty[]? Properties { get; private set; }

    /// <summary>
    /// The constraint properties from Extensions
    /// </summary>
    protected IConstraintProperty[]? Constraints { get; private set; }

    /// <summary>
    /// The ref types from the properties in Extensions
    /// </summary>
    protected List<AnySchemaType>? RefTypes { get; private set; }
    
    #endregion

    #region Methods

    /// <summary>
    /// Load the type with the schema, including properties, constraints and ref types
    /// </summary>
    public async Task LoadTypeAsync(SchemaContext context, NodeSchema schema, bool preload = false)
    {
        Extensions = null;
        Properties = null;
        Constraints = null;
        RefTypes = null;

        if (schema.Extensions != null)
        {
            Extensions = schema.Extensions;
            Properties = Extensions != null ? PropertyType.GetProperties<IProperty>(context, schema.Type, Extensions, fullConstraintList: IsValueType)?.ToArray() : null;

            if (Properties is { Length: > 0 })
            {
                Constraints = Properties.Where(p => p is IConstraintProperty).Cast<IConstraintProperty>().ToArray();
                foreach (var typeRef in Properties.Where(p => p is ITypeRefProperty && p.HasValue).Cast<ITypeRefProperty>())
                {
                    string? name = typeRef.GetValue<string>();
                    AnySchemaType? node = !string.IsNullOrWhiteSpace(name) ? await context.GetSchemaTypeAsync(name) : null;
                    if (node != null)
                    {
                        RefTypes ??= [];
                        RefTypes.Add(node);
                        node.AddRef(this);
                    }
                    else
                    {
                        Status = SchemaNodeStatus.WrongRefType;
                        context.LogWarning($"Failed to load ref type '{name}' for property '{typeRef.Name}' in schema '{Name}'");
                    }
                }
            }
        }
        Loaded = true;
        await LoadAsync(context, schema, preload);
    }

    /// <summary>
    /// Load the schema data
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="schema">The schema</param>
    /// <param name="preload">Whether during preload</param>
    public virtual Task LoadAsync(SchemaContext context, NodeSchema schema, bool preload = false) { 
        return Task.CompletedTask; 
    }

    public void ReleaseType()
    {
        if (RefTypes != null)
        {
            foreach (var node in RefTypes)
            {
                node.RemoveRef(this);
            }
        }
        Release();
    }
    
    /// <summary>
    /// Release the refs
    /// </summary>
    public virtual void Release() { }
    
    /// <summary>
    /// Gets the authentication policies with the scope
    /// </summary>
    public IEnumerable<PolicyItem> GetAuthPolicies(PolicyScope scope)
    {
        // root
        if (Namespace == null)
        {
            // use system instead
            if (this is TypeNamespace ns && ns.SchemaNodes[NS_SYSTEM] is TypeNamespace system)
            {
                foreach (var item in system.GetAuthPolicies(scope))
                    yield return item;
            }
        }
        // system don't check parent
        else if (!Name.Equals(NS_SYSTEM)) 
        {
            foreach (var item in Namespace.GetAuthPolicies(scope))
                yield return item;
        }

        if (Auth == null) yield break;
        {
            var item = Auth.Items.FirstOrDefault(p => p.Scope == scope);
            if (item != null) yield return item;
        }
    }

    /// <summary>
    /// Used by another node
    /// </summary>
    public void AddRef(AnySchemaType type)
    {
        // check compatibles, rare but important
        if (IsValueType && type is FunctionType { Args.Length: 1, Converter: true } func && func.Args[0].SchemaType == this && 
            func.ReturnNode != null && !CanBeUseAs(func.ReturnNode) && func.Converter == true)
        {
            // Means this type can be converted to func.ReturnNode via func
            _compatibles ??= [];
            _compatibles.TryAdd(func.ReturnNode, func);
        }
        
        // system types are not tracked
        if ((LoadState & SchemaLoadState.System) == SchemaLoadState.System && !(type is ArrayType arr && Name.Equals(arr.Element, StringComparison.OrdinalIgnoreCase))) return;
        UsedBy ??= new ConcurrentDictionary<AnySchemaType, bool>();
        UsedBy.TryAdd(type, true);
    }

    /// <summary>
    /// Used by an application field
    /// </summary>
    public void AddRef(AppFieldType type)
    {
        // system types are not tracked
        if ((LoadState & SchemaLoadState.System) == SchemaLoadState.System) return;
        UsedByApp ??= new ConcurrentDictionary<AppFieldType, bool>();
        UsedByApp.TryAdd(type, true);
    }

    /// <summary>
    /// Remove a ref from another node
    /// </summary>
    public void RemoveRef(AnySchemaType node)
    {
        if (node is FunctionType { ReturnNode: not null } func 
            && _compatibles != null
            && _compatibles.TryGetValue(func.ReturnNode!, out var f) && f == func)
        {
            _compatibles.TryRemove(func.ReturnNode!, out _);
        }
        UsedBy?.TryRemove(node, out _);
    }

    /// <summary>
    /// Add ref for an application workflow
    /// </summary>
    /// <param name="type"></param>

    public void AddRef(AppWorkflowType type)
    {
        // system types are not tracked
        if ((LoadState & SchemaLoadState.System) == SchemaLoadState.System) return;
        UsedByWorkflow ??= new ConcurrentDictionary<AppWorkflowType, bool>();
        UsedByWorkflow.TryAdd(type, true);
    }

    /// <summary>
    /// Remove ref for an application workflow
    /// </summary>
    /// <param name="type"></param>
    public void RemoveRef(AppWorkflowType type) => UsedByWorkflow?.TryRemove(type, out _);

    /// <summary>
    /// Remove ref for an application field
    /// </summary>
    /// <param name="type"></param>
    public void RemoveRef(AppFieldType type) => UsedByApp?.TryRemove(type, out _);

    public AnySchemaNode? CreateNode(object? value = null) => Type switch
    {
        SchemaType.Scalar => new ScalarTypeNode((ScalarType)this, value),
        SchemaType.Enum => new EnumTypeNode((EnumType)this, value),
        SchemaType.Struct => new StructTypeNode((StructType)this, value),
        SchemaType.Array => new ArrayTypeNode((ArrayType)this, value),
        SchemaType.Json => new JsonTypeNode((JsonType)this, value),
        _ => value is AnySchemaNode node ? node : null
    };

    /// <summary>
    /// Validate the value with the schema
    /// </summary>
    public virtual async Task<(AnySchemaNode?, JsonNode?)> ValidateValueAsync(SchemaContext context, JsonNode value, IReadOnlyList<IConstraintProperty>? constraints = null)
    {
        await Task.Yield();
        return (null, null);
    }

    /// <summary>
    /// Whether the schema type can be used as the other
    /// </summary>
    public virtual bool CanBeUseAs(AnySchemaType other, bool exactly = false)
        => this == other || Name.Equals(other.Name) || Name.Equals(NS_SYSTEM_OBJECT) || Name.Equals(NS_SYSTEM_JSON) ||
           other.Name.Equals(NS_SYSTEM_OBJECT) || other.Name.Equals(NS_SYSTEM_JSON) ||
           !exactly && _compatibles != null && (_compatibles.ContainsKey(other) || _compatibles.Keys.Any(k => k.CanBeUseAs(other, true)));

    /// <summary>
    /// Gets the array node that use this node as element
    /// </summary>
    public virtual ArrayType? GetArrayType(bool exactly = false) =>
        UsedBy?.Keys.FirstOrDefault(p => p is ArrayType array && array.ElementSchemaType == this) as ArrayType
        ?? (!exactly ? UsedBy?.Keys.FirstOrDefault(p => p is ArrayType { ElementSchemaType: not null } array 
                                                        && CanBeUseAs(array.ElementSchemaType)) as ArrayType : null); 
    
    /// <summary>
    /// Whether the type can be used as data index
    /// </summary>
    public virtual bool IsIndexable => false;
    
    /// <summary>
    /// Whether the new schema is valid for updating
    /// </summary>
    public virtual bool IsUpdatable(AnySchemaType other) => Type == other.Type;

    /// <summary>
    /// Release ref
    /// </summary>
    public void Dispose() => ReleaseType();

    /// <summary>
    /// Gets the depends schema nodes
    /// </summary>
    /// <returns></returns>
    public virtual IEnumerable<AnySchemaType> GetDependNodes()
    {
        yield break;
    }

    /// <summary>
    /// Gets all node schemas used by the node schema
    /// </summary>
    /// <returns></returns>
    public async Task<NodeSchema> GetNodeSchemas(SchemaContext ctx, NodeSchema? root = null, HashSet<string>? types = null, bool includeUsedBy = false, CancellationToken? cancellationToken = null)
    {
        if (!this.Loaded) await ctx.GetSchemaTypeAsync(this.Name);
        
        types ??= [];
        root ??= new NodeSchema
        {
            Name = "",
            Type = SchemaType.Namespace,
            Schemas = []
        };
        if (!types.Add(Name) || this is GenericType) return root;
        
        // install
        string[] paths = Name.Split('.', StringSplitOptions.RemoveEmptyEntries);
        string fullPath = string.Empty;
        NodeSchema parent = root;
        for (int i = 0; i < paths.Length - 1; i++)
        {
            string p = paths[i];
            fullPath = string.IsNullOrWhiteSpace(fullPath) ? p : $"{fullPath}.{p}";
                
            parent.Schemas ??= [];
            NodeSchema? sub = parent.Schemas.FirstOrDefault(s => fullPath.Equals(s.Name, StringComparison.OrdinalIgnoreCase));
            if (sub == null)
            {
                cancellationToken?.ThrowIfCancellationRequested();

                AnySchemaType type = await ctx.GetSchemaTypeAsync(fullPath) ?? new TypeNamespace { Name = fullPath };
                sub = type;
                parent.Schemas = parent.Schemas == null ? [sub!] : parent.Schemas.Append(sub!).ToArray();
            }
            parent = sub!;
        }

        NodeSchema schema = this!;
        if (includeUsedBy)
        {
            schema.UsedBy = UsedBy?.Keys.Select(p => p.Name).ToArray();
            schema.UsedByApp = UsedByApp?.Keys.Select(p => p.App).Distinct().ToArray();
            if (UsedByWorkflow is { IsEmpty: false })
                schema.UsedByApp = UsedByWorkflow.Keys.Select(p => p.App).Concat(schema.UsedByApp ?? []).Distinct().ToArray();
        }

        if (parent.Schemas == null || !parent.Schemas.Any(s => s.Name.Equals(schema.Name, StringComparison.OrdinalIgnoreCase)))
        {
            parent.Schemas ??= [];
            parent.Schemas = parent.Schemas.Append(schema).ToArray();
        }
        
        if (this is TypeNamespace ns)
        {
            foreach (var s in ns.Schemas)
            {
                cancellationToken?.ThrowIfCancellationRequested();
                var sns = await ctx.GetSchemaTypeAsync(s.Name);
                if (sns != null)
                    await sns.GetNodeSchemas(ctx, root, types, includeUsedBy, cancellationToken);
            }
        }

        // add dependencies
        foreach (AnySchemaType n in GetDependNodes())
        {
            cancellationToken?.ThrowIfCancellationRequested();
            await n.GetNodeSchemas(ctx, root, types, includeUsedBy, cancellationToken);
        }

        if (RefTypes != null)
        {
            foreach (var refType in RefTypes)
            {
                await refType.GetNodeSchemas(ctx, root, types, includeUsedBy, cancellationToken);
            }
        }

        return root;
    }

    #endregion
    
    #region Conversion

    /// <summary>
    /// Convert the schema to node
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static implicit operator AnySchemaType?(NodeSchema? schema)
    {
        if (schema == null) return null;
        return schema.Type switch
        {
            SchemaType.Namespace => new TypeNamespace { Name = schema.Name, Display = schema.Display, LoadState = schema.LoadState ?? SchemaLoadState.Server, SchemaProvider = schema.SchemaProvider },
            SchemaType.Scalar => new ScalarType { Name = schema.Name, Display = schema.Display, LoadState = schema.LoadState ?? SchemaLoadState.Server, SchemaProvider = schema.SchemaProvider },
            SchemaType.Enum => new EnumType { Name = schema.Name, Display = schema.Display, LoadState = schema.LoadState ?? SchemaLoadState.Server, SchemaProvider = schema.SchemaProvider },
            SchemaType.Struct => new StructType { Name = schema.Name, Display = schema.Display, LoadState = schema.LoadState ?? SchemaLoadState.Server, SchemaProvider = schema.SchemaProvider },
            SchemaType.Array => new ArrayType { Name = schema.Name, Display = schema.Display, LoadState = schema.LoadState ?? SchemaLoadState.Server, SchemaProvider = schema.SchemaProvider },
            SchemaType.Func => new FunctionType { Name = schema.Name, Display = schema.Display, LoadState = schema.LoadState ?? SchemaLoadState.Server, SchemaProvider = schema.SchemaProvider },
            SchemaType.Json => new JsonType{ Name = schema.Name, Display = schema.Display, LoadState = schema.LoadState ?? SchemaLoadState.Server, SchemaProvider = schema.SchemaProvider  },
            SchemaType.Event => new EventType{ Name = schema.Name, Display = schema.Display, LoadState = schema.LoadState ?? SchemaLoadState.Server, SchemaProvider = schema.SchemaProvider  },
            SchemaType.Workflow => new WorkflowType{ Name = schema.Name, Display = schema.Display, LoadState = schema.LoadState ?? SchemaLoadState.Server, SchemaProvider = schema.SchemaProvider  },
            SchemaType.Policy => new PolicyType{ Name = schema.Name, Display = schema.Display, LoadState = schema.LoadState ?? SchemaLoadState.Server, SchemaProvider = schema.SchemaProvider  },
            SchemaType.Recognizer => new RecognizerType{ Name = schema.Name, Display = schema.Display, LoadState = schema.LoadState ?? SchemaLoadState.Server, SchemaProvider = schema.SchemaProvider  },
            SchemaType.Property => new PropertyType{ Name = schema.Name, Display = schema.Display, LoadState = schema.LoadState ?? SchemaLoadState.Server, SchemaProvider = schema.SchemaProvider  },
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    /// <summary>
    /// Convert the node to schema
    /// </summary>
    public static implicit operator NodeSchema?(AnySchemaType? schema)
    {
        if (schema == null) return null;
        return schema.Type switch
        {
            SchemaType.Scalar => (schema as ScalarType),
            SchemaType.Enum => (schema as EnumType),
            SchemaType.Struct => (schema as StructType),
            SchemaType.Array => (schema as ArrayType),
            SchemaType.Func => (schema as FunctionType),
            SchemaType.Json => (schema as JsonType),
            SchemaType.Event => (schema as EventType),
            SchemaType.Workflow => (schema as WorkflowType),
            SchemaType.Policy => (schema as PolicyType),
            SchemaType.Recognizer => (schema as RecognizerType),
            SchemaType.Property => (schema as PropertyType),
            _ => (schema as TypeNamespace)
        };
    }
    
    protected NodeSchema ToSchema()
    {
        return new NodeSchema
        {
            Name = Name.ToLower(),
            Type = Type,
            Display = Display,
            LoadState = LoadState,
            Status = Status == SchemaNodeStatus.Ready ? null : Status,
            Auth = Auth?.Name,
            Used = IsUsed,
            Extensions = Extensions,
            Compatibles = _compatibles?.Select(p => new CompatibleSchema(p.Key.Name, p.Value.Name)).ToArray(),
        };
    }
    
    #endregion

    #region Utility

    private ConcurrentDictionary<AnySchemaType, FunctionType>? _compatibles;
    internal ConcurrentDictionary<AnySchemaType, bool>? UsedBy;
    internal ConcurrentDictionary<AppFieldType, bool>? UsedByApp;
    internal ConcurrentDictionary<AppWorkflowType,  bool>? UsedByWorkflow;

    #endregion
}