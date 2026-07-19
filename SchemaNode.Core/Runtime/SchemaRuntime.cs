using System.Collections.Concurrent;
using SchemaNode.Enum;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Schema;
using SchemaNode.Struct;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

/// <summary>
/// The schema run-time with all run-time schema information, such as the schema types, properties, and so on.
/// It will be built by the stage handlers in the build stage and used in the runtime stage.
/// Normally it'd be a singleton instance for one service.
/// </summary>
public class SchemaRuntime : ISchemaRuntime
{
    #region Implementation of ISchemaRuntime

    private (string kind, Type schemaType, Type[]? propertyTypes, IProperty[]? properties)[] _schemaKinds = [];
    private readonly object _schemaKindsLock = new();

    /// <summary>
    /// The current stage of the schema loading and runtime activation pipeline, it will be updated by the system and can be used to determine the current stage in the pipeline.
    /// </summary>
    public RuntimeStage Stage { get; set; } = RuntimeStage.SystemSchemaLoading;

    /// <inheritdoc/>
    public void RegisterSchemaKind(string kind, Type schemaType, Type[]? propertyTypes = null, IProperty[]? properties = null)
    {
        lock (_schemaKindsLock)
            _schemaKinds = _schemaKinds.Append((kind, schemaType, propertyTypes, properties)).ToArray();
    }

    /// <inheritdoc/>
    public IEnumerable<(string kind, Type schemaType)> GetSchemaKinds()
        => _schemaKinds.Select(k => (k.kind, k.schemaType));

    /// <inheritdoc/>
    public IEnumerable<Type> GetSchemaKindPropertyTypes(string kind)
        => _schemaKinds.FirstOrDefault(k => k.kind.Equals(kind, StringComparison.OrdinalIgnoreCase)).propertyTypes ?? [];

    /// <inheritdoc/>
    public T? GetSchemaKindProperty<T>(string kind) where T : class, IProperty
        => _schemaKinds.FirstOrDefault(k => k.kind.Equals(kind, StringComparison.OrdinalIgnoreCase)).properties
            ?.OfType<T>().FirstOrDefault();

    /// <inheritdoc/>
    public IEnumerable<T> GetSchemaKindProperties<T>(string kind) where T : class, IProperty
        => _schemaKinds.FirstOrDefault(k => k.kind.Equals(kind, StringComparison.OrdinalIgnoreCase)).properties
            ?.OfType<T>() ?? [];

    /// <inheritdoc/>
    public Type? GetSchemaKindPropertyTypeByName(string kind, string propertyName)
        => GetSchemaKindPropertyTypes(kind).FirstOrDefault(propType => propertyName.Equals(propType.GetPropertyName(), StringComparison.OrdinalIgnoreCase));

    #endregion

    #region Node Type

    private readonly ConcurrentDictionary<string, Type> _nodeTypes = new (StringComparer.OrdinalIgnoreCase);
    
    /// <summary>
    /// Register the node type for schema kind
    /// </summary>
    public void RegisterNodeType(string kind, Type nodeType) => _nodeTypes.TryAdd(kind, nodeType);
    
    /// <summary>
    /// Gets the node type for the schema kind
    /// </summary>
    public Type? GetNodeType(string kind) => _nodeTypes.GetValueOrDefault(kind);

    #endregion

    #region System Node Schema

    private readonly ConcurrentDictionary<Type, string> _typeCache = new();
    private readonly ConcurrentDictionary<string, string> _arrayCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly NodeSchema _rootSchema = new()
    {
        Name = "",
        Kind = SCHEMA_KIND_NAMESPACE,
        Schemas = [],
    };

    /// <summary>
    /// Gets system schema from C# type
    /// </summary>
    /// <param name="type">The C# type</param>
    /// <returns></returns>
    public string? GetTypeSchema(Type type)
    {
        if (_typeCache.TryGetValue(type, out string? schemaName))
            return schemaName;
        
        // Handle generic types, e.g. List<string> => system.list<system.string>
        TypeDetail detail = type.GetTypeDetail();
        if (detail.IsGenericParameter) return null;
        if (detail.IsGenericType)
        {
            schemaName = GetTypeSchema(detail.CoreType.GetGenericTypeDefinition());
            if (schemaName == null) return null;
            Type[] args = type.GetGenericArguments();
            string[] genericArgs = new string[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                string? n = GetTypeSchema(args[i]);
                if (n == null) return null;
                genericArgs[i] = n;
            }
            schemaName = $"{schemaName}<{string.Join(", ", genericArgs)}>";
            return detail.AnyArray ? GetSystemArraySchema(schemaName) : schemaName;
        }
        if (detail.CoreType != type && _typeCache.TryGetValue(detail.CoreType, out schemaName))
            return detail.AnyArray  ? GetSystemArraySchema(schemaName) : schemaName;
        return null;
    }

    /// <summary>
    /// Save a node schema as system-defined schema
    /// </summary>
    internal void SaveSystemSchema(NodeSchema schema)
    {
        // special for array
        if (schema.Kind == SCHEMA_KIND_ARRAY && schema.GetProperty<ArrayProperty>()?.Value is {} arraySchema)
            _arrayCache[arraySchema.Element] = schema.FullName;

        string schemaName = schema.FullName.ToLowerInvariant();
        NodeSchema root = _rootSchema;
        string fullPath = "";

        SpanReader reader = schemaName;
        while(reader.NextNamespace())
        {
            string ns = fullPath;
            string part = reader.Current.ToString();
            fullPath = !string.IsNullOrWhiteSpace(fullPath) ? $"{fullPath}.{part}" : part;

            NodeSchema? node = root.Schemas?.FirstOrDefault(x => x.Name.Equals(part, StringComparison.OrdinalIgnoreCase));
            if (node == null)
            {
                if (schemaName == fullPath)
                {
                    // Target node: add it
                    root.Schemas = root.Schemas != null ? root.Schemas.Concat([schema]).ToArray() : [schema];
                }
                else
                {
                    // Intermediate namespace: create it
                    node = new NodeSchema
                    {
                        Name = part,
                        Namespace = ns,
                        Kind = SCHEMA_KIND_NAMESPACE,
                        Schemas = [],
                    };
                    node.SetProperty<Display, LocaleString>(node.FullName);
                    root.Schemas = root.Schemas != null ? root.Schemas.Concat([node]).ToArray() : [node];
                    root = node;
                    root.Schemas ??= [];
                }
            }
            else if (schemaName != fullPath)
            {
                root = node;
                root.Schemas ??= [];
            }
            else if (node.Kind != schema.Kind || node.Type != null && schema.Type != null && node.Type != schema.Type)
            {
                // Conflict with existing schema
                throw new InvalidOperationException($"System schema name conflict: {schema.FullName} with kind {schema.Kind} conflicts with existing kind {node.Kind}");
            }
            // override the extension properties
            else if (node.Kind != SCHEMA_KIND_NAMESPACE)
            {
                node.CombineProperties(schema, this, SCHEMA_KIND_NODE);
            }
        }
        
        // Cache the type to name mapping for quick lookup
        if (schema.Type != null)
            _typeCache.TryAdd(schema.Type, schemaName);
        if (schema.Equivalents == null) return;
        foreach (Type eq in schema.Equivalents)
            _typeCache[eq] = schemaName;
    }

    /// <summary>
    /// Gets a system-defined node schema by name
    /// </summary>
    public NodeSchema? GetSystemSchema(string schemaName)
    {
        NodeSchema? node = _rootSchema;
        SpanReader reader = schemaName;
        while (node != null && reader.NextNamespace())
        {
            ReadOnlySpan<char> part = reader.Current;
            NodeSchema? curr = null;
            
             // Generic Types
            if (part.StartsWith('<'))
                return node.Clone(this);
            
            if (node.Schemas != null)
            {
                foreach (NodeSchema schema in node.Schemas)
                {
                    if (!part.SeqEquals(schema.Name, StringComparison.OrdinalIgnoreCase)) continue;
                    curr = schema;
                    break;
                }
            }
            node = curr;
        }
        return node?.Clone(this, true);
    }

    /// <summary>
    /// Gets the generic arguments of the system schema name
    /// </summary>
    public string[] GetSystemSchemaGenericArguments(string schemaName)
    {
        NodeSchema? node = _rootSchema;
        SpanReader reader = schemaName;
        while (node != null && reader.NextNamespace())
        {
            ReadOnlySpan<char> part = reader.Current;
            NodeSchema? curr = null;
            
            // Generic Types
            if (part.StartsWith('<'))
            {
                if (!part.EndsWith('>'))
                    throw new Exception($"Invalid generic type syntax for {schemaName}");
                part = part[1..^1];

                List<string> genParams = [];
                SpanReader genericReader = part;
                string key = part.ToString();

                while(genericReader.NextGenericParam())
                {
                    ReadOnlySpan<char> genericParam = genericReader.Current;
                    genParams.Add(genericParam.ToString());
                }
                return genParams.ToArray();
            }
            
            if (node.Schemas != null)
            {
                foreach (NodeSchema schema in node.Schemas)
                {
                    if (!part.SeqEquals(schema.Name, StringComparison.OrdinalIgnoreCase)) continue;
                    curr = schema;
                    break;
                }
            }
            node = curr;
        }

        return [];
    }
    
    /// <summary>
    /// Try gets the array schema for the given element type. The element type should be the full name of the type, e.g. "system.string" for string array.
    /// </summary>
    public string? GetSystemArraySchema(string elementType, bool noGeneric = false) => 
        _arrayCache.GetValueOrDefault(elementType) ?? (!noGeneric ? $"{NS_SYSTEM_LIST}<{elementType.ToLowerInvariant()}>"  : null);
    
    #endregion

    #region Node Types
    
    /// <summary>
    /// The root namespace
    /// </summary>
    public readonly NamespaceType RootNamespace = new ();
    
    #endregion
    
    #region Runtime Items

    /// <summary>
    /// The context item
    /// </summary>
    private readonly ConcurrentDictionary<Type, object> _runtimeItems = []; 
    
    /// <summary>
    /// Sets the context item
    /// </summary>
    public void SetRuntimeItem(Type type, object? value)
    {
        if (_runtimeItems.ContainsKey(type))
        {
            if (_runtimeItems[type] == value) return;
            if (_runtimeItems.TryRemove(type, out object? org) && org != value)
                (org as IDisposable)?.Dispose();
        }
        if(value != null)
            _runtimeItems[type] = value;
    }

    /// <summary>
    /// Sets the context item
    /// </summary>
    public void SetRuntimeItem<T>(T? value) => SetRuntimeItem(typeof(T), value);
    
    /// <summary>
    /// Gets the context item as data node
    /// </summary>
    public object? GetRuntimeItem(Type type) => _runtimeItems.GetValueOrDefault(type);

    /// <summary>
    /// Gets the context item with the given type
    /// </summary>
    public T? GetRuntimeItem<T>() where T: class => GetRuntimeItem(typeof(T)) as T;

    /// <summary>
    /// Gets or creates the context item
    /// </summary>
    public T GetOrAddRuntimeItem<T>(Func<T> factory) where T : class => (T)_runtimeItems.GetOrAdd(typeof(T), _ => factory());

    /// <summary>
    /// Gets or creates the context item
    /// </summary>
    public T GetOrAddRuntimeItem<T>() where T : class, new() => (T)_runtimeItems.GetOrAdd(typeof(T), _ => new T());
    
    #endregion
}