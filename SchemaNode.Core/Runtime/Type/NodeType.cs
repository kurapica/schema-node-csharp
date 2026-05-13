using System.Collections.Concurrent;
using SchemaNode.Context;
using SchemaNode.Property;
using SchemaNode.Schema;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Property.Function;
using SchemaNode.Property.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory schema representation
/// </summary>
public abstract class NodeType: INodeReferences, IDisposable, INodeError
{
    #region Properties

    /// <summary>
    /// The namespace
    /// </summary>
    public string Name => Schema != null ? GenericParams != null ? $"{Schema.FullName}<{string.Join(", ", GenericParams.Select(g => g.Name))}>" : Schema.FullName : string.Empty;

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
    public Type? Provider => Schema?.Provider;
    
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
    
    #region Generic
    
    /// <summary>
    /// The generics
    /// </summary>
    internal GenericParameter[]? Generics { get; private set; }
    
    /// <summary>
    /// The generic type map for generic type definition, like T => string, used for generic type definition, which is important for generic type loading and refactor. The key is the generic type name, and the value is the actual node type.
    /// </summary>
    private ConcurrentDictionary<string, NodeType>? GenericMap { get; set; }

    /// <summary>
    /// The generic type lookup
    /// </summary>
    private ConcurrentDictionary<string, NodeType>.AlternateLookup<ReadOnlySpan<char>>? GenericMapLookup { get; set; }

    /// <summary>
    /// The generic parameters
    /// </summary>
    public IReadOnlyList<NodeType>? GenericParams { get; private set; }
    
    /// <summary>
    /// The node type is generic template
    /// </summary>
    public bool IsGeneric => GenericParams == null && Generics is { Length: > 0 };

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
    public virtual Task LoadAsync(SchemaContext context) => Task.CompletedTask;

    /// <summary>
    /// Release the references
    /// </summary>
    public virtual void Release() { }

    #endregion
    
    #region Methods

    /// <summary>
    /// Load the type with the schema, including properties, constraints and ref types
    /// </summary>
    internal virtual async Task LoadTypeAsync(SchemaContext context, NodeSchema schema, NodeType[]? genericParams = null)
    {
        ReleaseType();
        Error = null;
        GenericParams = genericParams is { Length: > 0 } ? genericParams : null;

        Schema = schema;
        List<IProperty> props = schema.GetProperties(context.Runtime.GetSchemaKindProperties(SCHEMA_KIND_NODE));

        foreach (IProperty prop in props.ToArray())
        {
            if (prop.GetValue<ExtensibleSchema>(true) is not { } s || schema.Kind.Equals(s.SchemaKind)) continue;
            props.AddRange(s.GetProperties(context.Runtime.GetSchemaKindProperties(schema.Kind)));
        }

        _props = props.Count > 0 ? props.ToArray() : null;
        Generics = GetProperty<Generics>()?.Value;

        Loaded = true;
        await LoadAsync(context);

        // Loading schema properties after loading, to avoid cycle ref
        List<NodeType> refTypes = [];
        if (GenericParams == null)
        {
            foreach (ITypeRefProperty prop in props.Cast<ITypeRefProperty>())
            {
                string? name = prop.GetValue<string>();
                NodeType? node = !string.IsNullOrWhiteSpace(name) ? await context.GetNodeTypeAsync(name) : null;
                if (node != null)
                {
                    refTypes.Add(node);
                }
                else
                {
                    Error = ErrorCodes.WRONG_REF_TYPE;
                    context.LogWarning($"Failed to load ref type '{name}' for property '{name}' in schema '{Name}'");
                }
            }
        }

        // Update the properties
        _refTypes = refTypes.Count > 0 ? refTypes.ToArray() : null;
        
        // Register UsedBy
        foreach (NodeType referenceType in GenericParams ?? GetReferenceTypes())
        {
            if (referenceType is not GenericType)
                referenceType.AddUsedBy(this);
        }
    }

    internal void ReleaseType()
    {
        foreach (NodeType node in GenericParams ?? GetReferenceTypes())
            node.RemoveUsedBy(this);
        Release();
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
    /// Gets the property value with given type, returns null if not exist or not match type
    /// </summary>
    public T? GetPropertyValue<T>()
    {
        IProperty? prop = _props?.FirstOrDefault(p => p.Type.IsAssignableTo(typeof(T)));
        return prop != null ? prop.GetValue<T>(true) : default(T?);
    }

    /// <summary>
    /// Gets the generic map
    /// </summary>
    internal NodeType? GetGenericType(ReadOnlySpan<char> name)
    {
        if (GenericMap == null) return null;
        GenericMapLookup ??= GenericMap.GetAlternateLookup<ReadOnlySpan<char>>();
        return GenericMapLookup.Value.TryGetValue(name, out NodeType? node) ? node : null;
    }

    /// <summary>
    /// Sets the generic map
    /// </summary>
    internal void SetGenericType(ReadOnlySpan<char> name, NodeType node)
    {
        GenericMap ??= [];
        GenericMap[name.ToString()] = node;
    }
    
    /// <summary>
    /// Gets all generated generic types
    /// </summary>
    /// <returns></returns>
    internal IEnumerable<NodeType> GetGenericTypes()
    {
        if (GenericMap == null) yield break;
        foreach (NodeType g in GenericMap.Values)
            yield return g;
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
        NodeSchema parent = root;
        SpanReader reader = new SpanReader(Name);
        while (reader.NextNamespace())
        {
            parent.Schemas ??= [];
            string matched = reader.Matched.ToString();
            NodeSchema? sub = parent.Schemas.FirstOrDefault(s => matched.Equals(s.Name, StringComparison.OrdinalIgnoreCase));
            if (sub == null)
            {
                cancellationToken?.ThrowIfCancellationRequested();
                sub = (await context.GetNodeTypeAsync(matched))?.Schema ?? new NodeSchema{ Name = matched.GetSchemaName(), Namespace = matched.GetNamespace(), Kind = SCHEMA_KIND_NAMESPACE };
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

        // add references
        foreach (NodeType n in GetReferenceTypes())
        {
            cancellationToken?.ThrowIfCancellationRequested();
            await n.GetNodeSchemas(context, root, types, includeUsedBy, cancellationToken);
        }

        return root;
    }

    #endregion

    #region UsedBy
    
    /// <summary>
    /// Used by unknown objects
    /// </summary>
    public virtual void AddUsedBy<T>(T usedBy)
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
    public virtual void RemoveUsedBy<T>(T usedBy)
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

    #region Implementation of INodeReference

    /// <inheritdoc/>
    public virtual IEnumerable<NodeType> GetReferenceTypes()
    {
        if (_refTypes == null) yield break;
        foreach (var t in _refTypes)
            yield return t;
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
    internal ArrayType? ArrayType { get; private set; }
    
    #endregion
    
    #region Override Methods

    internal override async Task LoadTypeAsync(SchemaContext context, NodeSchema schema, NodeType[]? genericParams = null)
    {
        await base.LoadTypeAsync(context, schema, genericParams);
        _constraints = GetProperties<IConstraintProperty>().ToArray();
    }

    /// <summary>
    /// Used by unknown objects
    /// </summary>
    public override void AddUsedBy<T>(T usedBy)
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

        base.AddUsedBy(usedBy);
    }

    /// <summary>
    /// Remove a ref from another node
    /// </summary>
    public override void RemoveUsedBy<T>(T usedBy)
    {
        if (usedBy is FunctionType { ReturnNode: not null } func 
            && _isAssignableTo != null
            && _isAssignableTo.TryGetValue(func.ReturnNode!, out var f) && f == func)
        {
            _isAssignableTo.TryRemove(func.ReturnNode!, out _);
        }

        if (usedBy != null && usedBy.Equals(ArrayType)) ArrayType = null;
        
        base.RemoveUsedBy(usedBy);
    }

    #endregion
    
    #region Abstract Methods
    
    /// <summary>
    /// Validate the value with the schema
    /// </summary>
    public abstract Task<DataNode> ValidateValueAsync(SchemaContext context, object? value);

    /// <summary>
    /// Generate the data node from value, no validation will be performed
    /// </summary>
    public abstract DataNode ParseValue(object? value);
        
    /// <summary>
    /// Gets value type through path reader
    /// </summary>
    public virtual ValueType? GetAccessValueType(ReadOnlySpan<char> path) => path.IsEmpty ? this : null;

    /// <summary>
    /// The value type is assignable to other value type
    /// </summary>
    public virtual bool IsAssignableTo(ValueType other)
        => this == other || Name.Equals(other.Name) || 
           Kind.Equals(SCHEMA_KIND_OBJECT)  || 
           other.Kind.Equals(SCHEMA_KIND_OBJECT) ||
           _isAssignableTo != null && 
           (_isAssignableTo.ContainsKey(other) || 
            _isAssignableTo.Keys.Any(k => k.IsAssignableTo(other)));

    /// <summary>
    /// The value type is assignable from other value type
    /// </summary>
    public bool IsAssignableFrom(ValueType other) => other.IsAssignableFrom(this);

    #endregion
}