using System.Collections.Concurrent;
using SchemaNode.Context;
using SchemaNode.Property;
using SchemaNode.Schema;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Property.Function;
using SchemaNode.Property.Core;
using SchemaNode.Struct;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory schema representation
/// </summary>
public abstract class NodeType: INodeReferences, IDisposable, INodeError
{
    #region Fields
    
    // generic
    private ConcurrentDictionary<string, NodeType>? _genericMap;
    private ConcurrentDictionary<string, NodeType>.AlternateLookup<ReadOnlySpan<char>>? _genericMapLookup;

    // properties
    private IProperty[]? _props;
    private NodeType[]? _refTypes;

    // used by
    private ConcurrentDictionary<NodeType, bool>? _usedBy;
    private ConcurrentDictionary<Type, ConcurrentDictionary<object, bool>>? _usedByOther;

    #endregion

    #region Properties
    
    /// <summary>
    /// The parent
    /// </summary>
    public NamespaceType? Namespace { get; private set; }

    /// <summary>
    /// The node schema
    /// </summary>
    protected NodeSchema? Schema { get; private set; }
    
    /// <summary>
    /// The namespace
    /// </summary>
    public string Name => Schema != null 
        ? GenericParams is { Count: > 0 }
            ? $"{Schema.FullName}<{string.Join(", ", GenericParams.Select(g => g.Name))}>" 
            : Schema.FullName 
        : string.Empty;

    /// <summary>
    /// The node schema type
    /// </summary>
    public string Kind => Schema?.Kind ?? SCHEMA_KIND_NODE;

    /// <summary>
    /// The schema node error code
    /// </summary>
    public string? Error { get; set; }
    
    /// <summary>
    /// The scheme provider used to load the node
    /// </summary>
    public Type? Provider => Schema?.Provider;
    
    /// <summary>
    /// The type is loaded
    /// </summary>
    public bool Loaded { get; internal set; }

    /// <summary>
    /// The load state
    /// </summary>
    public SchemaLoadState LoadState { get; internal set; } = SchemaLoadState.Service;

    /// <summary>
    /// Whether the node is used
    /// </summary>
    public virtual bool IsUsed => _usedBy is { IsEmpty: false } || _usedByOther is { IsEmpty: false } && _usedByOther.Any(k => !k.Value.IsEmpty);

    #endregion
    
    #region Generic
    
    /// <summary>
    /// The generics
    /// </summary>
    public GenericParameter[]? Generics { get; private set; }

    /// <summary>
    /// The generic parameters
    /// </summary>
    public IReadOnlyList<NodeType>? GenericParams { get; private set; }
    
    /// <summary>
    /// The node type is generic template
    /// </summary>
    public bool IsGeneric => GenericParams is not { Count: > 0 } && Generics is { Length: > 0 };

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

    /// <summary>
    /// Gets the csharp type
    /// </summary>
    public virtual Type? GetCsharpType() => Schema?.Type;
    
    /// <summary>
    /// Gets the csharp type with nullable modifier
    /// </summary>
    public Type? GetCsharpType(bool? nullable) => nullable == true ? GetCsharpType()?.GetNullableType() : GetCsharpType();

    #endregion
    
    #region Methods

    /// <summary>
    /// Load the type with the schema, including properties, constraints and ref types
    /// </summary>
    internal virtual async Task LoadTypeAsync(SchemaContext context, NodeSchema schema, NodeType[]? genericParams = null)
    {
        // reset
        ReleaseType();
        Error = null;
        
        // load basic info
        Namespace = !string.IsNullOrWhiteSpace(schema.Namespace) ? await context.GetNodeTypeAsync<NamespaceType>(schema.Namespace) : null;
        Schema = schema;
        GenericParams = genericParams is { Length: > 0 } ? genericParams : null;

        // load properties
        List<IProperty> props = schema.GetProperties(context.Runtime.GetSchemaKindProperties(SCHEMA_KIND_NODE)).ToList();
        int max = props.Count;
        for(int i = 0; i < max; i++)
        {
            if (props[i].GetValue<ExtensibleSchema>(true) is not { } s || schema.Kind.Equals(s.SchemaKind)) continue;
            props.AddRange(s.GetProperties(context.Runtime.GetSchemaKindProperties(schema.Kind)));
        }

        _props = props.Count > 0 ? props.ToArray() : null;
        Generics = GetProperty<Generics>()?.Value;

        Loaded = true;
        await LoadAsync(context);
        
        (_refTypes, string? error) = await schema.LoadPropertiesAsync(context, props, this as ValueType);
        Error ??= error;
        
        // Loading schema properties after loading, to avoid cycle ref
        _refTypes = GenericParams == null ? _refTypes : null;
        
        // Register UsedBy
        foreach (NodeType referenceType in GenericParams ?? GetReferenceTypes())
        {
            if (referenceType is not GenericType)
                referenceType.AddUsedBy(this);
        }
    }

    private void ReleaseType()
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
    /// Gets the property by property name
    /// </summary>
    public IProperty? GetProperty(string propertyName) => _props?.FirstOrDefault(p => p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Gets the generic map
    /// </summary>
    internal NodeType? GetGenericType(ReadOnlySpan<char> name)
    {
        if (_genericMap == null) return null;
        _genericMapLookup ??= _genericMap.GetAlternateLookup<ReadOnlySpan<char>>();
        return _genericMapLookup.Value.TryGetValue(name, out NodeType? node) ? node : null;
    }

    /// <summary>
    /// Sets the generic map
    /// </summary>
    internal void SetGenericType(ReadOnlySpan<char> name, NodeType node)
    {
        _genericMap ??= [];
        _genericMap[name.ToString()] = node;
    }
    
    /// <summary>
    /// Gets all generated generic types
    /// </summary>
    /// <returns></returns>
    internal IEnumerable<NodeType> GetGenericTypes()
    {
        if (_genericMap == null) yield break;
        foreach (NodeType g in _genericMap.Values)
            yield return g;
    }
    
    /// <summary>
    /// Gets the node schema
    /// </summary>
    public NodeSchema? GetNodeSchema(ISchemaRuntime? runtime = null) => Schema?.Clone(runtime);

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
        SpanReader reader = Name;
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

    /// <summary>
    /// Gets the nodes that reference this node, only return node types, other types are not tracked and will not be returned
    /// </summary>
    public IEnumerable<NodeType> GetUsedBy() => _usedBy?.Keys ?? [];

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
public abstract class ValueType : NodeType, IValueTypeAccess
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
    /// The array type
    /// </summary>
    public ArrayType? ArrayType { get; internal set; }
    
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
                when func.Args[0].ValueType == this && 
                     !IsAssignableTo(func.Return) && 
                     func.GetProperty<Converter>() != null:
                // Means this type can be converted to func.ReturnNode via func
                _isAssignableTo ??= [];
                _isAssignableTo.TryAdd(func.Return, func);
                break;
            case ArrayType arr when Name.Equals(arr.Element?.Name, StringComparison.OrdinalIgnoreCase):
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
        if (usedBy is FunctionType func 
            && _isAssignableTo != null
            && _isAssignableTo.TryGetValue(func.Return, out var f) && f == func)
        {
            _isAssignableTo.TryRemove(func.Return, out _);
        }

        if (usedBy != null && usedBy.Equals(ArrayType)) ArrayType = null;
        
        base.RemoveUsedBy(usedBy);
    }

    #endregion

    #region Methods
    
    /// <summary>
    /// Generate data node from object and validate the value
    /// </summary>
    public async Task<DataNode> ValidateValueAsync(SchemaContext context, object? value)
    {
        DataNode? result = null;
        if (value is DataNode node)
        {
            if (node.Type == this || IsAssignableTo(node.Type))
                result = node;
            else
                value = node.TryGetValue(out object? v) ? v : null;
        }
        
        if (result == null)
        {
            result = Create();
            if (value != null && !result.TrySetValue(value))
            {
                result.SetViolated(Kind);
                return result;
            }
        }
    
        // Node type validation
        await ValidateNodeAsync(context, result);
        
        // apply constraints
        List<IProperty>? errors = null;
        List<IProperty>? passed = null;
        foreach (IConstraintProperty constraint in Constraints.Where(c => c.HasValue))
        {
            if (await constraint.ValidateAsync(context, result) == false)
            {
                errors ??= [];
                errors.Add(constraint);
            }
            else
            {
                passed ??= [];
                passed.Add(constraint);
            }
        }
        if (errors != null || passed != null)
            result.SetViolated(errors, passed);
        
        return result;
    }

    #endregion
    
    #region Abstract

    /// <summary>
    /// Generate the data node from the node type
    /// </summary>
    public abstract DataNode Create();

    /// <summary>
    /// Generate the data node with given value
    /// </summary>
    public DataNode From(object? value)
    {
        var node = Create();
        node.TrySetValue(value);
        return node;
    }
    
    #endregion

    #region Virtual
    /// <summary>
    /// Whether the type can be used as data index
    /// </summary>
    public virtual bool IsIndexable => false;

    /// <summary>
    /// Validate the data node
    /// </summary>
    protected virtual Task ValidateNodeAsync(SchemaContext context, DataNode node) => Task.CompletedTask;

    /// <summary>
    /// Gets value type through path reader
    /// </summary>
    public virtual ValueType? GetAccessValueType(string path) => string.IsNullOrWhiteSpace(path) || path.SequenceEqual(NODE_SELF) ? this : null;

    /// <summary>
    /// Gets sub entries
    /// </summary>
    public virtual IEnumerable<Entry<string>> GetSubEntries()
    {
        yield break;
    }

    /// <summary>
    /// Has sub entries
    /// </summary>
    public virtual bool HasSubEntries => false;
    
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

    #endregion
}