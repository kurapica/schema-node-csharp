using SchemaNode.Context;
using SchemaNode.Property;
using SchemaNode.Schema;
using System.Collections.Concurrent;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Property.Function;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory schema representation
/// </summary>
public abstract class NodeType: IDisposable
{
    #region Properties

    /// <summary>
    /// The namespace
    /// </summary>
    public string Name => Schema?.FullName ?? string.Empty;

    /// <summary>
    /// The node schema type
    /// </summary>
    public string Kind => Schema?.Kind ?? SCHEMA_KIND_NODE;

    /// <summary>
    /// The node schema
    /// </summary>
    internal NodeSchema? Schema { get; private set; }

    /// <summary>
    /// The schema node error code
    /// </summary>
    public string? Error { get; protected set; }
        
    /// <summary>
    /// The scheme provider used to load the node
    /// </summary>
    internal Type? Provider { get; set; }
    
    /// <summary>
    /// The type is loaded
    /// </summary>
    internal bool Loaded { get; set; }

    /// <summary>
    /// The load state
    /// </summary>
    internal SchemaLoadState LoadState { get; set; } = SchemaLoadState.Service;

    /// <summary>
    /// Whether the node is used
    /// </summary>
    public virtual bool IsUsed => _usedBy is { IsEmpty: false } || _usedByOther is { IsEmpty: false } && _usedByOther.Any(k => !k.Value.IsEmpty);

    #endregion
    
    #region Fields
    
    // properties
    private IProperty[]? _props;
    private NodeType[]? _refTypes;

    // used by
    private ConcurrentDictionary<NodeType, bool>? _usedBy;
    private ConcurrentDictionary<Type, ConcurrentDictionary<object, bool>>? _usedByOther;

    #endregion

    #region Abstract

    /// <summary>
    /// Load the schema data
    /// </summary>
    /// <param name="context">The schema context</param>
    public virtual Task LoadAsync(SchemaContext context) => Task.CompletedTask;

    /// <summary>
    /// Release the references
    /// </summary>
    public virtual void Release() { }

    /// <summary>
    /// Gets the depends schema nodes
    /// </summary>
    /// <returns></returns>
    public virtual IEnumerable<NodeType> GetDependNodes()
    {
        yield break;
    }

    #endregion
    
    #region Methods

    /// <summary>
    /// Load the type with the schema, including properties, constraints and ref types
    /// </summary>
    internal virtual async Task LoadTypeAsync(SchemaContext context, NodeSchema schema)
    {
        ReleaseType();
        Error = null;
        
        Schema = schema;
        List<IProperty> props = [];
        
        // loading node properties
        foreach (Type pType in context.Runtime.GetSchemaKindProperties(SCHEMA_KIND_NODE))
        {
            IProperty? prop = schema.GetProperty(pType);
            if (prop is not { HasValue: true }) continue;
            props.Add(prop);
        }
        _props = props.Count > 0 ? props.ToArray() : null;
        
        Loaded = true;
        await LoadAsync(context);
        
        // Loading schema properties after loading, to avoid cycle ref
        List<NodeType> refTypes = [];
        foreach (IProperty prop in props.ToArray())
        {
            await SaveRef(prop);
            
            if (prop.GetValue<ExtensibleSchema>(true) is not { } s) continue;
            foreach (Type spType in context.Runtime.GetSchemaKindProperties(schema.Kind))
            {
                IProperty? sProp = s.GetProperty(spType);
                if (sProp is not { HasValue: true }) continue;
                props.Add(sProp);
                await SaveRef(sProp);
            }
        }
        
        // Update the properties
        _props = props.Count > 0 ? props.ToArray() : null;
        _refTypes = refTypes.Count > 0 ? refTypes.ToArray() : null;

        async Task SaveRef(IProperty prop)
        {
            if (prop is ITypeRefProperty typeRefProp)
            {
                string? name = typeRefProp.GetValue<string>();
                NodeType? node = !string.IsNullOrWhiteSpace(name) ? await context.GetNodeTypeAsync(name) : null;
                if (node != null)
                {
                    refTypes.Add(node);
                    node.AddRef(this);
                }
                else
                {
                    Error = ErrorCodes.WRONG_REF_TYPE;
                    context.LogWarning($"Failed to load ref type '{name}' for property '{name}' in schema '{Name}'");
                }
            }
        }
    }

    internal void ReleaseType()
    {
        Release();
        if (_refTypes == null || _refTypes.Length == 0) return;
        foreach (NodeType node in _refTypes)
            node.RemoveRef(this);
    }

    /// <summary>
    /// Gets the property with given type
    /// </summary>
    public IProperty? GetProperty<T>() where T : class, IProperty => _props?.OfType<T>().FirstOrDefault();

    /// <summary>
    /// Gets the constraints
    /// </summary>
    public IEnumerable<T> GetProperties<T>() => _props?.OfType<T>() ?? [];
    
    /// <summary>
    /// Gets the property value with given type, returns null if not exist or not match type
    /// </summary>
    public T? GetPropertyValue<T>()
    {
        IProperty? prop = _props?.FirstOrDefault(p => p.Type.IsAssignableTo(typeof(T)));
        return prop != null ? prop.GetValue<T>(true) : default(T?);
    }

    /// <summary>
    /// Gets all node schemas used by the node schema
    /// </summary>
    /// <returns></returns>
    public async Task<NodeSchema> GetNodeSchemas(SchemaContext context, 
        NodeSchema? root = null, 
        HashSet<string>? types = null, 
        bool includeUsedBy = false, 
        CancellationToken? cancellationToken = null)
    {
        if (!Loaded) await context.GetNodeTypeAsync(Name);
        
        types ??= [];
        root ??= new NodeSchema
        {
            Name = "",
            Kind = SCHEMA_KIND_NAMESPACE,
            Schemas = []
        };
        if (Schema == null || !types.Add(Name) || this is GenericType) return root;
        
        // install
        string fullPath = string.Empty;
        NodeSchema parent = root;
        foreach (string p in Name.SplitTypeName())
        {
            fullPath = string.IsNullOrWhiteSpace(fullPath) ? p : $"{fullPath}.{p}";
                
            parent.Schemas ??= [];
            NodeSchema? sub = parent.Schemas.FirstOrDefault(s => fullPath.Equals(s.Name, StringComparison.OrdinalIgnoreCase));
            if (sub == null)
            {
                cancellationToken?.ThrowIfCancellationRequested();
                sub = (await context.GetNodeTypeAsync(fullPath))?.Schema ?? new NodeSchema{ Name = fullPath.GetSchemaName(), Namespace = fullPath.GetNamespace(), Kind = SCHEMA_KIND_NAMESPACE };
                parent.Schemas = parent.Schemas == null ? [sub] : parent.Schemas.Append(sub).ToArray();
            }
            parent = sub;
        }

        NodeSchema schema = Schema.Clone(context.Runtime);
        if (includeUsedBy)
        {
            schema.UsedBy = _usedBy?.Keys.Select(p => p.Name).ToArray();
        }

        if (parent.Schemas == null || !parent.Schemas.Any(s => s.Name.Equals(schema.Name, StringComparison.OrdinalIgnoreCase)))
        {
            parent.Schemas ??= [];
            parent.Schemas = parent.Schemas.Append(schema).ToArray();
        }
        
        if (this is NamespaceType ns)
        {
            foreach (NodeSchema s in ns.GetNodeSchemas())
            {
                cancellationToken?.ThrowIfCancellationRequested();
                var sns = await context.GetNodeTypeAsync(s.Name);
                if (sns != null)
                    await sns.GetNodeSchemas(context, root, types, includeUsedBy, cancellationToken);
            }
        }

        // add dependencies
        foreach (NodeType n in GetDependNodes())
        {
            cancellationToken?.ThrowIfCancellationRequested();
            await n.GetNodeSchemas(context, root, types, includeUsedBy, cancellationToken);
        }

        if (_refTypes != null)
        {
            foreach (var refType in _refTypes)
                await refType.GetNodeSchemas(context, root, types, includeUsedBy, cancellationToken);
        }

        return root;
    }

    #endregion

    #region Reference
    
    /// <summary>
    /// Used by unknown objects
    /// </summary>
    public virtual void AddRef<T>(T usedBy)
    {
        if ((LoadState & SchemaLoadState.System) == SchemaLoadState.System) return;
        
        // track the used by for schema types, which is important for schema update and refactor
        if (usedBy is NodeType any)
        {
            _usedBy ??= new ConcurrentDictionary<NodeType, bool>();
            _usedBy.TryAdd(any, true);
            return;
        }
        
        // system types are not tracked

        _usedByOther ??= new ConcurrentDictionary<Type, ConcurrentDictionary<object, bool>>();
        _usedByOther.GetOrAdd(typeof(T), _ => new ConcurrentDictionary<object, bool>()).TryAdd(usedBy!, true);
    }

    /// <summary>
    /// Remove a ref from another node
    /// </summary>
    public virtual void RemoveRef<T>(T usedBy)
    {
        if (usedBy is NodeType any)
        {
            _usedBy?.TryRemove(any, out _);
            return;
        }
        
        if (_usedByOther != null && _usedByOther.TryGetValue(typeof(T), out ConcurrentDictionary<object, bool>? dict))
            dict.TryRemove(usedBy!, out _);
    }

    #endregion
    
    #region Implementation of IDispse
    
    /// <summary>
    /// Release ref
    /// </summary>
    public void Dispose() => ReleaseType();
    
    #endregion
}

/// <summary>
/// Represents the value schema type
/// </summary>
public abstract class ValueType : NodeType
{
    #region Fields
    
    private ConcurrentDictionary<ValueType, FunctionType>? _isAssignableTo;
    private IConstraintProperty[]? _constraints;

    #endregion
    
    #region Properties
    
    /// <summary>
    /// Gets the constraints
    /// </summary>
    public IEnumerable<IConstraintProperty> Constraints => _constraints?.AsEnumerable() ?? [];

    /// <summary>
    /// Whether the type can be used as data index
    /// </summary>
    public virtual bool IsIndexable => false;
    
    /// <summary>
    /// The array type
    /// </summary>
    public ArrayType? ArrayType { get; private set; }
    
    #endregion
    
    #region Override Methods

    internal override async Task LoadTypeAsync(SchemaContext context, NodeSchema schema)
    {
        await base.LoadTypeAsync(context, schema);
        _constraints = GetProperties<IConstraintProperty>().ToArray();
    }

    /// <summary>
    /// Used by unknown objects
    /// </summary>
    public override void AddRef<T>(T usedBy)
    {
        switch (usedBy)
        {
            // check compatibles, rare but important
            case FunctionType { Args.Length: 1, Converter: true } func 
                when func.Args[0].SchemaType == this && 
                     func.ReturnNode != null && 
                     !IsAssignableTo(func.ReturnNode) && 
                     func.GetProperty<Converter>() != null:
                // Means this type can be converted to func.ReturnNode via func
                _isAssignableTo ??= [];
                _isAssignableTo.TryAdd(func.ReturnNode, func);
                break;
            case ArrayType arr when Name.Equals(arr.Element, StringComparison.OrdinalIgnoreCase):
                ArrayType ??= arr;
                break;
        }

        base.AddRef(usedBy);
    }

    /// <summary>
    /// Remove a ref from another node
    /// </summary>
    public override void RemoveRef<T>(T usedBy)
    {
        if (usedBy is FunctionType { ReturnNode: not null } func 
            && _isAssignableTo != null
            && _isAssignableTo.TryGetValue(func.ReturnNode!, out var f) && f == func)
        {
            _isAssignableTo.TryRemove(func.ReturnNode!, out _);
        }

        if (usedBy != null && usedBy.Equals(ArrayType)) ArrayType = null;
        
        base.RemoveRef(usedBy);
    }

    #endregion
    
    #region Abstract Methods
    
    /// <summary>
    /// Validate the value with the schema
    /// </summary>
    public virtual Task<DataNode?> ValidateValueAsync(SchemaContext context, object value) => Task.FromResult<DataNode?>(null);

    /// <summary>
    /// The value type is assignable to other value type
    /// </summary>
    public virtual bool IsAssignableTo(ValueType other)
        => this == other || Name.Equals(other.Name) || Kind.Equals(SCHEMA_KIND_OBJECT)  || other.Kind.Equals(SCHEMA_KIND_OBJECT) ||
           _isAssignableTo != null && (_isAssignableTo.ContainsKey(other) || _isAssignableTo.Keys.Any(k => k.IsAssignableTo(other)));

    /// <summary>
    /// The value type is assignable from other value type
    /// </summary>
    public bool IsAssignableFrom(ValueType other) => other.IsAssignableFrom(this);

    /// <summary>
    /// Generate the node type with generic parameters
    /// </summary>
    public virtual Task<NodeType?> GetGenericTypeAsync(SchemaContext context, string[] types) => Task.FromResult<NodeType?>(null);

    #endregion
}