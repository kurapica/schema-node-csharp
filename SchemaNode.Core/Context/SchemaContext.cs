using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Schema.Provider;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
using ArrayType = SchemaNode.Runtime.ArrayType;
using NamespaceType = SchemaNode.Runtime.NamespaceType;
using NodeType = SchemaNode.Runtime.NodeType;
using ValueType = SchemaNode.Runtime.ValueType;
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable AccessToModifiedClosure
// ReSharper disable RedundantNameQualifier
// ReSharper disable VariableHidesOuterVariable

namespace SchemaNode.Context;

/// <summary>
/// The schema context
/// </summary>
public class SchemaContext(IServiceProvider services, ISchemaRuntime runtime): ISchemaContext, IDisposable
{
    #region Properties

    /// <summary>
    /// The services provider
    /// </summary>
    public IServiceProvider Services { get; } = services;
    
    /// <summary>
    /// The schema runtime
    /// </summary>
    public ISchemaRuntime Runtime { get; } = runtime;

    /// <summary>
    /// Works in system only mode
    /// </summary>
    internal bool SystemMode { get; set; }

    /// <summary>
    /// Gets the logger
    /// </summary>
    ILogger Logger => _loggerThunk.Value;
    readonly Lazy<ILogger> _loggerThunk = new(services.GetRequiredService<ILogger<SchemaContext>>);

    /// <summary>
    /// The context item
    /// </summary>
    readonly ConcurrentDictionary<Type, object> _contextItems = [];     
    
    /// <summary>
    /// The system access
    /// </summary>
    public SystemAccess System => Services.GetRequiredService<SystemAccess>();
    
    #endregion
    
    #region Service Resolution

    /// <summary>
    /// Gets the required service
    /// </summary>
    public T GetRequiredService<T>() where T: notnull => Services.GetRequiredService<T>();
    
    /// <summary>
    /// Gets the required service
    /// </summary>
    public object GetRequiredService(Type serviceType) => Services.GetRequiredService(serviceType);
    
    /// <summary>
    /// Gets the service
    /// </summary>
    public T? GetService<T>() where T: notnull => Services.GetService<T>();
    
    /// <summary>
    /// Gets the service
    /// </summary>
    public object? GetService(Type serviceType) => Services.GetService(serviceType);
    
    /// <summary>
    /// Gets the services
    /// </summary>
    public IEnumerable<T> GetServices<T>() where T: notnull => Services.GetServices<T>();
    
    /// <summary>
    /// Gets the services
    /// </summary>
    public IEnumerable<object?> GetServices(Type serviceType) => Services.GetServices(serviceType);

    #endregion

    #region Log

    /// <summary>
    /// Log debug message
    /// </summary>
    public void LogDebug(string message, params object?[] args) => Logger.LogDebug(message, args);
    
    /// <summary>
    /// Log information message
    /// </summary>
    public void LogInformation(string message, params object?[] args) => Logger.LogInformation(message, args);
    
    /// <summary>
    /// Log warning message
    /// </summary>
    public void LogWarning(string message, params object?[] args) => Logger.LogWarning(message, args);
    
    /// <summary>
    /// Log error message
    /// </summary>
    public void LogError(Exception ex, string message, params object?[] args) => Logger.LogError(ex, message, args);

    /// <summary>
    /// Log error message
    /// </summary>
    public void LogError(string message, params object?[] args) => Logger.LogError(message, args);
    
    #endregion
    
    #region Methods

    /// <summary>
    /// Gets the node schema by name
    /// </summary>
    public async Task<NodeSchema?> GetNodeSchemaAsync(string fullName)
        => (await GetNodeTypeAsync(fullName))?.GetNodeSchema(Runtime);

    /// <summary>
    /// Gets the schema node type by name
    /// </summary>
    public async Task<NodeType?> GetNodeTypeAsync(string fullName, IReadOnlyList<GenericParameter>? generics = null, IReadOnlyList<NodeType>? genericParams = null, bool reload = false)
    {
        // generic type for simple
        if (generics?.FindIndex(g => g.Name.Equals(fullName, StringComparison.OrdinalIgnoreCase)) is {} gIdx and >= 0)
            return genericParams?.ElementAtOrDefault(gIdx) ?? new GenericType { Name = generics[gIdx].Name };

        // registered type
        SchemaRuntime schemaRuntime = Runtime as SchemaRuntime ?? throw new InvalidOperationException();
        SpanReader spans = fullName;
        
        // try use existed node type directly, reload means only need load it when it existed
        NodeType? node = await LoadNodeTypeAsync(schemaRuntime.RootNamespace, spans);
        while (node != null && spans.NextNamespace())
            node = await LoadNodeTypeAsync(node, spans);
        return node;

        async Task<NodeType?> LoadNodeTypeAsync(NodeType node, SpanReader spans)
        {
            ReadOnlySpan<char> next = spans.Current;
            NamespaceType? parent = node as NamespaceType;
            NodeType? result = node;
            if (!next.IsEmpty)
            {
                // Generic Types
                if (next.StartsWith('<'))
                {
                    if (!spans.IsEnd || !next.EndsWith('>'))
                    {
                        LogError("Invalid generic type format for {schemaName}", fullName);
                        return null;
                    }
                    next = next[1..^1];

                    // Check cache, allow duplicate if next contains different spaces (e.g. List<T> vs List< T >), keep it simple
                    if (node.GetGenericType(next) is { } genType) return genType;

                    List<NodeType> genParams = [];
                    SpanReader genericReader = next;
                    string key = next.ToString();

                    // Convert <T1, T2> to [T1, T2]
                    while(genericReader.NextGenericParam())
                    {
                        ReadOnlySpan<char> genericParam = genericReader.Current;
                        NodeType? type = !genericParam.IsEmpty ? await GetNodeTypeAsync(genericParam.ToString(), generics, genParams) : null;
                        if (type == null) return null;
                        genParams.Add(type);
                    }

                    if (node.Generics == null || node.Generics.Count != genParams.Count)
                    {
                        LogError("Generic type count mismatch for {schemaName}, expected {expected} but got {actual}", fullName, node.Generics?.Count ?? 0, genParams.Count);
                        return null;
                    }

                    // Create generic type
                    genType = ActivatorUtilities.CreateInstance(Services, node.GetType()) as NodeType;
                    if (genType == null)
                    {
                        LogError("Generic type {schemaName} load failed", fullName);
                        return null;
                    }
                    await genType.LoadTypeAsync(this, node.GetNodeSchema(schemaRuntime)!, genParams.ToArray());
                    node.SetGenericType(key, genType);
                    return genType;
                }
                
                // Get loaded node type
                result = parent?.GetNodeType(next);
            }
            // reload means don't load it if not existed
            if (result == null && reload || result?.Loaded == true && !(spans.IsEnd && reload))
                return result;
            
            // loading
            string nextVal = next.IsEmpty ? "" : next.ToString();
            NodeSchema? schema = await LoadNodeSchemaAsync(parent != result ? parent : null, nextVal);
            if (schema == null) return null;

            // node type
            Type nodeType = schemaRuntime.GetNodeType(schema.Kind) ?? typeof(NodeType);
            result ??= ActivatorUtilities.CreateInstance(Services, nodeType) as NodeType;
            if (result == null)
            {
                LogError("[Runtime]Schema Type {schemaName} load failed", schema.FullName);
                return null;
            }
            
            // cache by segment name (next), because result.Name is empty until LoadTypeAsync sets Schema
            NodeSchema[]? schemas = schema.Schemas;
            schema.Schemas = null;
            if (parent != result)
            {
                parent?.SaveNodeSchema(schema);
                parent?.SaveNodeType(nextVal, result);
            }

            // Load the schema
            LogDebug("[Runtime]Schema Type {schemaName} loading", schema.FullName);
            await result.LoadTypeAsync(this, schema);
            
            // Save sub-namespaces for the namespace
            if (result is NamespaceType ns && schemas is { Length: > 0 })
                foreach (NodeSchema s in schemas)
                    ns.SaveNodeSchema(s);
            
            // Generic Types Reloading
            foreach (NodeType g in result.GetGenericTypes())
                await g.LoadTypeAsync(this, schema.Clone(schemaRuntime), g.GenericParams!.ToArray());

            LogDebug("[Runtime]Schema Type {schemaName} working", schema.FullName);
            return result;
        }
        
        async Task<NodeSchema?> LoadNodeSchemaAsync(NamespaceType? @namespace, string name)
        {
            // get loaded schema from namespace if not in reload mode
            NodeSchema? schema = reload ? null : @namespace?.GetNodeSchema(name);
            if (schema != null) return schema;
            
            // system schema
            string schemaName = $"{@namespace?.Name}.{name}".Trim('.');
            schema = SetSchemaState(schemaRuntime.GetSystemSchema(schemaName), SchemaLoadState.System);
            if (SystemMode) return schema;

            // 3rd schema provider
            foreach (INodeSchemaProvider provider in GetServices<INodeSchemaProvider>())
            {
                try
                {
                    NodeSchema[] loadSchemas = await provider.LoadSchemaAsync([schemaName]);
                    if (loadSchemas.Length == 0) continue;
                    NodeSchema loadSchema = SetSchemaState(loadSchemas[0], SchemaLoadState.Service, provider.GetType())!;

                    // check && combine
                    if (schema == null)
                    {
                        schema = loadSchema;
                        continue;
                    }
                    schema.LoadState |= loadSchema.LoadState;
                    schema.Provider ??= loadSchema.Provider;
                    
                    // CombineProperties extensions
                    schema.CombineProperties(loadSchema, schemaRuntime, schema.Kind);

                    if (!loadSchema.Kind.Equals(SCHEMA_KIND_NAMESPACE, StringComparison.OrdinalIgnoreCase) ||
                        loadSchema.Schemas == null || loadSchema.Schemas.Length == 0) continue;
                    
                    if (schema.Schemas == null || schema.Schemas.Length == 0)
                    {
                        schema.Schemas = loadSchema.Schemas;
                        continue;
                    }
                    
                    // combine namespaces
                    List<NodeSchema>? otherSchemas = null;
                    foreach (NodeSchema otherSchema in loadSchema.Schemas)
                    {
                        int index = Array.FindIndex(schema.Schemas, s => s.Name.Equals(otherSchema.Name, StringComparison.OrdinalIgnoreCase));
                        if (index >= 0)
                        {
                            if (schema.Schemas[index].Kind.Equals(otherSchema.Kind, StringComparison.OrdinalIgnoreCase))
                                schema.Schemas[index].CombineProperties(otherSchema, schemaRuntime, otherSchema.Kind);
                        }
                        else
                        {
                            otherSchemas ??= [];
                            otherSchemas.Add(otherSchema);
                        }
                    }
                    if (otherSchemas != null)
                        schema.Schemas = schema.Schemas.Concat(otherSchemas).ToArray();
                }
                catch(Exception e)
                {
                    LogError(e, $"Failed to load schema '{schemaName}' from schema provider '{provider.GetType().FullName}'.");
                }
            }
            return schema;
        }

        NodeSchema? SetSchemaState(NodeSchema? schema, SchemaLoadState loadState, Type? provider = null)
        {
            schema?.Provider = provider;
            schema?.LoadState = loadState;
            if (schema?.Kind != SCHEMA_KIND_NAMESPACE || schema.Schemas == null || schema.Schemas.Length == 0) return schema;
            foreach (NodeSchema s in schema.Schemas)
                SetSchemaState(s, loadState, provider);
            return schema;
        }
    }

    /// <summary>
    /// Gets the schema node of specific type
    /// </summary>
    public async Task<T?> GetNodeTypeAsync<T>(string schemaName, IReadOnlyList<GenericParameter>? generics = null, IReadOnlyList<NodeType>? genericParams = null, bool reload = false) where T : NodeType
        => await GetNodeTypeAsync(schemaName, generics, genericParams, reload) as T;
    
    /// <summary>
    /// Gets the value type's array type
    /// </summary>
    /// <param name="elementType"></param>
    /// <returns></returns>
    public async Task<ArrayType?> GetArrayNodeTypeAsync(ValueType elementType)
        => elementType as ArrayType ?? (elementType.ArrayType ?? await GetNodeTypeAsync<ArrayType>((Runtime as SchemaRuntime)!.GetSystemArraySchema(elementType.Name)!));
    
    /// <summary>
    /// Gets the schema node from value
    /// </summary>
    public async Task<DataNode?> GetSchemaNodeAsync(object? value, ValueType? expectedType = null, bool onlyValid = false)
    {
        if (value == null) return expectedType?.From(null);

        if (expectedType == null)
        {
            switch (value)
            {
                case JsonValue jsonValue:
                {
                    var (v, t) = jsonValue.ParseValueAndType();
                    string? schemaType = t?.GetSchemaType();
                    expectedType = !string.IsNullOrEmpty(schemaType) ? await GetNodeTypeAsync<ValueType>(schemaType) : null;
                    break;
                }
                case JsonNode:
                case JsonElement:
                    break; // can't handle it without expected type
                default:
                {
                    var cacheItem = GetSchemeCacheItem().TypeCache;
                    Type valueType = value.GetType();
                    if (cacheItem.TryGetValue(valueType, out ValueType? cached))
                    {
                        expectedType = cached;
                    }
                    else
                    {
                        string? name = (Runtime as SchemaRuntime)!.GetTypeSchema(valueType);
                        if (!string.IsNullOrWhiteSpace(name))
                            expectedType = await GetNodeTypeAsync<ValueType>(name);
                        expectedType ??= new GenericType();
                        cacheItem[valueType] = expectedType!;
                    }
                    break;
                }
            }
        }

        if (expectedType != null && expectedType is not GenericType)
        {
            try
            {
                if (!onlyValid) return expectedType.From(value);
                DataNode node = await expectedType.ValidateValueAsync(this, value);
                return node.IsValid ? node : null;
            }
            catch (Exception e)
            {
                LogError(e, "Failed to convert value to expected type {expectedType}", expectedType.Name);
                return null;
            }
        }
        return null;
    }

    #endregion

    #region Context Items

    /// <summary>
    /// Sets the context item
    /// </summary>
    public void SetContextItem(Type type, object? value)
    {
        if (_contextItems.ContainsKey(type))
        {
            if (_contextItems[type] == value) return;
            if (_contextItems.TryRemove(type, out object? org) && org != value)
                (org as IDisposable)?.Dispose();
        }
        if(value != null)
            _contextItems[type] = value;
    }

    /// <summary>
    /// Sets the context item
    /// </summary>
    public void SetContextItem<T>(T? value) where T : class => SetContextItem(typeof(T), value);

    /// <summary>
    /// Gets context item result, try to get from context item provider if not exist in context items
    /// </summary>
    object? GetContextItemResult(Type type, bool asDataNode = false)
    {
        if (_contextItems.TryGetValue(type, out object? result)) return result;
        
        // try with context item provider
        (string SchemaType, Type ProviderType, Type ItemType)? info = GetRequiredService<SchemaContextItemProvider>().GetProviderType(type);
        if (info == null) return null;
        return GetService(info.Value.ProviderType) is ISchemaContextItemProvider { HasItem: true } provider && provider.TryGetItem(out object? item)
            ? asDataNode ? GetNodeTypeAsync<ValueType>(info.Value.SchemaType).GetAwaiter().GetResult()?.From(item) : item
            : null;
    }
    
    /// <summary>
    /// Gets the context item with the given type
    /// </summary>
    public T? GetContextItem<T>() where T : class
    {
        object? result = GetContextItemResult(typeof(T));
        return result as T ?? default(T?);
    }
    
    /// <summary>
    /// Gets the context item as data node
    /// </summary>
    public DataNode? GetContextItem(Type type) => GetContextItemResult(type, true) as DataNode;

    /// <summary>
    /// Gets the context item
    /// </summary>
    internal DataNode? GetContextItem(string contextItem)
    {
        string[] split = contextItem.Split('.', 2);
        if (split.Length == 0) return null;
        (string SchemaType, Type ProviderType, Type ItemType)? info = GetRequiredService<SchemaContextItemProvider>().GetProviderType(split[0]);
        if (info == null) return null;
        DataNode? result = GetService(info.Value.ProviderType) is ISchemaContextItemProvider { HasItem: true } provider && provider.TryGetItem(out object? item)
            ? GetNodeTypeAsync<ValueType>(info.Value.SchemaType).GetAwaiter().GetResult()?.From(item)
            : null;
        return result != null && split.Length > 1 ? result.GetAccessValue(split[1]) as DataNode : result;
    }
    
    /// <summary>
    /// Copys the schema context item from source to target
    /// </summary>
    public void CopySchemaContextItem(SchemaContext source)
    {
        // save direct context items
        foreach (var pair in source._contextItems)
            SetContextItem(pair.Key, pair.Value);
        
        // save context items for providers
        foreach ((string SchemaType, Type ProviderType, Type ItemType) info in GetRequiredService<SchemaContextItemProvider>().GetProviderTypes)
        {
            if (source.GetService(info.ProviderType) is ISchemaContextItemProvider { HasItem: true } provider && provider.TryGetItem(out object? item))
            {
                if (item != null)
                    (GetService(info.ProviderType) as ISchemaContextItemProvider)?.TrySetItem(item);
            }
        }
    }

    /// <summary>
    /// Gets or creates the context item
    /// </summary>
    public T GetOrAddContextItem<T>(Func<T> factory) where T : class => (T)_contextItems.GetOrAdd(typeof(T), _ => factory());

    /// <summary>
    /// Gets or creates the context item
    /// </summary>
    public T GetOrAddContextItem<T>() where T : class, new() => (T)_contextItems.GetOrAdd(typeof(T), _ => new T());

    #endregion

    #region Implementation of IDisposable

    public void Dispose()
    {
        foreach (KeyValuePair<Type, object> item in _contextItems)
            (item.Value as IDisposable)?.Dispose();
    }

    #endregion

    #region Scheme Cache Item

    SchemeCacheItem GetSchemeCacheItem() => GetOrAddContextItem(() => new SchemeCacheItem(new ConcurrentDictionary<Type, ValueType>()));
    record class SchemeCacheItem(ConcurrentDictionary<Type, ValueType> TypeCache);

    #endregion
}

/// <summary>
/// The system node type access
/// </summary>
public class SystemAccess
{
    /// <summary>
    /// The system namespace self
    /// </summary>
    public Runtime.NamespaceType Self { get; internal set; } = null!;
    
    /// <summary>
    /// THe bool type
    /// </summary>
    public Runtime.BoolType Bool { get; internal set; } = null!;

    /// <summary>
    /// The string type
    /// </summary>
    public Runtime.StringType String { get; internal set; } = null!;

    /// <summary>
    /// The decimal type
    /// </summary>
    public Runtime.DecimalType Decimal { get; internal set; } = null!;

    /// <summary>
    /// The int type
    /// </summary>
    public Runtime.IntType Int { get; internal set; } = null!;

    /// <summary>
    /// The date type
    /// </summary>
    public Runtime.DateType Date { get; internal set; } = null!;

    /// <summary>
    /// The context type
    /// </summary>
    public Runtime.StructType Context { get; internal set; } = null!;
}