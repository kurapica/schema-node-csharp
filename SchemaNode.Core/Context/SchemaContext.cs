using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchemaNode.Enum;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Service;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
using ArrayType = SchemaNode.Runtime.ArrayType;
using NamespaceType = SchemaNode.Runtime.NamespaceType;
using NodeType = SchemaNode.Runtime.NodeType;
using ValueType = SchemaNode.Runtime.ValueType;
// ReSharper disable UnusedAutoPropertyAccessor.Global

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
        => (await GetNodeTypeAsync<NamespaceType>(fullName.GetNamespace()))?.GetNodeSchema(fullName.GetSchemaName());

    /// <summary>
    /// Gets the schema node type by name
    /// </summary>
    public async Task<NodeType?> GetNodeTypeAsync(string fullName, GenericParameter[]? generics = null, bool reload = false)
    {
        // generic type
        if (generics?.FirstOrDefault(g => g.Name.Equals(fullName, StringComparison.OrdinalIgnoreCase)) is { } generic)
            return new GenericType{ Name = generic.Name };
        
        // registered type
        SchemaRuntime schemaRuntime = Runtime as SchemaRuntime ?? throw new InvalidOperationException();
        SpanReader spans = fullName;
        NodeType? node = await LoadNodeTypeAsync(schemaRuntime.RootNamespace, spans);
        while (node != null && spans.NextNamespace())
            node = await LoadNodeTypeAsync(node, spans);

        return node;

        async Task<NodeType?> LoadNodeTypeAsync(NodeType node, SpanReader spans)
        {
            // Convert <T1, T2> to [T1, T2]
            ReadOnlySpan<char> next = spans.Current;
            NamespaceType? parent = node as NamespaceType;
            NodeType? result = node;
            if (!next.IsEmpty)
            {
                // Generic Types
                if (next.StartsWith('<'))
                {
                    if (!next.EndsWith('>'))
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

                    while(genericReader.NextGenericParam())
                    {
                        ReadOnlySpan<char> genericParam = genericReader.Current;
                        NodeType? type = !genericParam.IsEmpty ? await GetNodeTypeAsync(genericParam.ToString()) : null;
                        if (type == null)
                        {
                            LogError("Empty generic type parameter for {schemaName}", fullName);
                            return null;
                        }
                        genParams.Add(type);
                    }

                    if (node.Generics == null || node.Generics.Length != genParams.Count)
                    {
                        LogError("Generic type count mismatch for {schemaName}, expected {expected} but got {actual}", fullName, node.Generics?.Length ?? 0, genParams.Count);
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
                else if (parent == null)
                {
                    return null;
                }
                else
                {
                    result = parent.GetNodeType(next);
                }
            }
            
            // loading
            if (result is not { Loaded: true } || reload && spans.IsEnd)
            {
                string nextVal = next.IsEmpty ? "" : next.ToString();
                NodeSchema? schema = await LoadNodeSchemaAsync(parent != result ? parent : null, nextVal);
                if (schema == null) return null;

                // node type
                Type? nodeType = schemaRuntime.GetNodeType(schema.Kind);
                if (nodeType == null) return null;

                result ??= ActivatorUtilities.CreateInstance(Services, nodeType) as NodeType;
                if (result == null)
                {
                    LogError("[Runtime]Schema Type {schemaName} load failed", schema.FullName);
                    return null;
                }
                
                // cache by segment name (next), because result.Name is empty until LoadTypeAsync sets Schema
                if (parent != result)
                    parent?.SaveNodeType(nextVal, result);

                // Load the schema
                LogDebug("[Runtime]Schema Type {schemaName} loading", schema.FullName);

                await result.LoadTypeAsync(this, schema);
                
                // Namespace
                if (result is NamespaceType ns && schema.Schemas is { Length: > 0 })
                    foreach (NodeSchema s in schema.Schemas)
                        ns.SaveNodeSchema(s);
                
                // Generic Types Reloading
                foreach (NodeType g in result.GetGenericTypes())
                    await g.LoadTypeAsync(this, schema.Clone(schemaRuntime), g.GenericParams!.ToArray());

                LogDebug("[Runtime]Schema Type {schemaName} working", schema.FullName);
            }

            return result;
        }
        
        async Task<NodeSchema?> LoadNodeSchemaAsync(NamespaceType? @namespace, string name)
        {
            NodeSchema? schema = @namespace?.GetNodeSchema(name);
            if (schema != null && schema.Kind != SCHEMA_KIND_NAMESPACE) return schema;
            
            string schemaName = $"{@namespace?.Name}.{name}".Trim('.');
            schema = SetSchemaState(schemaRuntime.GetSystemSchema(schemaName), SchemaLoadState.System);
            if (SystemMode) return schema;

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
                    
                    // Combine
                    schema.CombineExtensions(loadSchema, schemaRuntime);

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
                            schema.Schemas[index].CombineExtensions(otherSchema, schemaRuntime);
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
            
            if (schema != null) @namespace?.SaveNodeSchema(schema);
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
    public async Task<T?> GetNodeTypeAsync<T>(string schemaName, GenericParameter[]? generics = null, bool reload = false) where T : NodeType
        => await GetNodeTypeAsync(schemaName, generics, reload) as T;
    
    /// <summary>
    /// Gets the value type's array type
    /// </summary>
    /// <param name="elementType"></param>
    /// <returns></returns>
    public async Task<ArrayType?> GetArrayNodeTypeAsync(ValueType elementType)
        => elementType as ArrayType ?? (elementType.ArrayType ?? await GetNodeTypeAsync<ArrayType>((Runtime as SchemaRuntime)!.GetSystemArraySchema(elementType.Name)!));
    
    #endregion
    
    #region Context Items
    
    /// <summary>
    /// Sets the context item
    /// </summary>
    public void SetContextItem<T>(T? value)
    {
        if (value == null)
        {
            if (_contextItems.TryRemove(typeof(T), out object? org))
                (org as IDisposable)?.Dispose();
        }
        else
        {
            _contextItems[typeof(T)] = value;
        }
    }

    /// <summary>
    /// Sets the context item
    /// </summary>
    public void SetContextItem(Type type, object? value)
    {
        if (value == null)
        {
            if (_contextItems.TryRemove(type, out object? org))
                (org as IDisposable)?.Dispose();
        }
        else
        {
            _contextItems[type] = value;
        }
    }
    
    /// <summary>
    /// Gets the context item
    /// </summary>
    public T? GetContextItem<T>() where T : class => _contextItems.TryGetValue(typeof(T), out object? value) ? value as T : null;
    
    /// <summary>
    /// Gets the context item
    /// </summary>
    public object? GetContextItem(Type type) => _contextItems.TryGetValue(type, out object? value) ? value : null;
    
    /// <summary>
    /// Try gets the context item
    /// </summary>
    public bool TryGetContextItem<T>(out T? value) where T : class
    {
        if (_contextItems.TryGetValue(typeof(T), out object? obj) && obj is T t)
        {
            value = t;
            return true;
        }
        value = null;
        return false;
    }
    
    /// <summary>
    /// Try gets the context item
    /// </summary>
    public bool TryGetContextItem(Type type, out object? value)
    {
        return _contextItems.TryGetValue(type, out value);
    }

    /// <summary>
    /// Gets or creates the context item
    /// </summary>
    public T GetOrCreateContextItem<T>(Func<T> factory) where T : class
    {
        return (T)_contextItems.GetOrAdd(typeof(T), _ => factory());
    }

    /// <summary>
    /// Gets or creates the context item
    /// </summary>
    public T GetOrCreateContextItem<T>() where T : class, new()
    {
        return (T)_contextItems.GetOrAdd(typeof(T), _ => new T());
    }

    #endregion

    #region Implementation of IDisposable

    public void Dispose()
    {
        foreach (KeyValuePair<Type, object> item in _contextItems)
            (item.Value as IDisposable)?.Dispose();
    }

    #endregion
}

/// <summary>
/// The system node type access
/// </summary>
public class SystemAccess
{
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
}