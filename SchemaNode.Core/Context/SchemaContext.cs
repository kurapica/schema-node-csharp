using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchemaNode.Enum;
using SchemaNode.Property.Schema;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Service;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
using NamespaceType = SchemaNode.Runtime.NamespaceType;
using NodeType = SchemaNode.Runtime.NodeType;

// ReSharper disable VariableHidesOuterVariable

namespace SchemaNode.Context;

/// <summary>
/// The schema context
/// </summary>
public class SchemaContext(IServiceProvider services, ISchemaRuntime runtime): ISchemaContext, IDisposable
{
    #region Fields

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
    public async Task<NodeType?> GetNodeTypeAsync(string fullName, GenericParameter[]? genericParameters = null, bool reload = false)
    {
        // generic type
        if (genericParameters?.FirstOrDefault(g => g.Name.Equals(fullName, StringComparison.OrdinalIgnoreCase)) is { } generic)
            return new GenericType{ Name = generic.Name };
        
        // registered type
        SchemaRuntime runtime = Runtime as  SchemaRuntime ?? throw new InvalidOperationException();
        string[] parts = fullName.SplitTypeName();
        NodeType? node = await LoadNodeTypeAsync(runtime.RootNamespace, null, reload && parts.Length == 0);
        for (int i = 1; i <= parts.Length; i++)
        {
            if (node is not NamespaceType ns) return null;
            node = await LoadNodeTypeAsync(ns, parts[i - 1], reload && parts.Length == i);
        }

        return node;

        async Task<NodeType?> LoadNodeTypeAsync(NamespaceType parent, string? next = null, bool reload = false)
        {
            // Convert path<T1, T2> to path, [T1, T2]
            string[]? generic = null;
            if (next != null && Regex.IsMatch(next, REGEX_GENERIC_IMPLEMENT))
            {
                Match match = Regex.Match(next, REGEX_GENERIC_IMPLEMENT);
                next = match.Groups[1].Value;
                generic = match.Groups[2].Value.Split(",", StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim()).ToArray();
            }
            
            NodeType? result = next != null ? parent.GetNodeType(next) : parent;
            NodeSchema? schema = result?.Schema;
            if (reload || result is not { Loaded: true })
            {
                schema = await LoadNodeSchemaAsync(parent != result ? parent : null, next ?? "");
                if (schema == null) return null;

                // node type
                Type? nodeType = runtime.GetNodeType(schema.Kind);
                if (nodeType == null) return null;

                result ??= ActivatorUtilities.CreateInstance(Services, nodeType) as NodeType;
                if (result == null)
                {
                    LogError("[Runtime]Schema Type {schemaName} load failed", schema.FullName);
                    return null;
                }
                
                // cache by segment name (next), because result.Name is empty until LoadTypeAsync sets Schema
                if (parent != result)
                    parent.SaveNodeType(next!, result);

                // Load the schema
                LogDebug("[Runtime]Schema Type {schemaName} loading", schema.FullName);

                await result.LoadTypeAsync(this, schema);
                
                // Namespace
                if (result is NamespaceType ns && schema.Schemas is { Length: > 0 })
                {
                    foreach (NodeSchema s in schema.Schemas)
                        ns.SaveNodeSchema(s);
                }
                
                // Generic Types Reloading
                if (result.GenericMap is { Count: > 0 })
                {
                    foreach (NodeType g in result.GenericMap.Values)
                        await g.LoadTypeAsync(this, schema.Clone(runtime), g.Generics!.ToArray());
                }

                LogDebug("[Runtime]Schema Type {schemaName} working", schema.FullName);
            }

            if (generic is not { Length: > 0 }) return result;

            #region Generics Type
            
            string key = string.Join(',', generic);
            if (result.GenericMap != null && result.GenericMap.TryGetValue(key, out NodeType? genericType))
                return genericType;
            
            NodeType[] genericTypes = new NodeType[generic.Length];
            for (int i = 0; i < generic.Length; i++)
            {
                NodeType? type = !string.IsNullOrWhiteSpace(generic[i]) ? await GetNodeTypeAsync(generic[i]) : null;
                if (type == null)
                {
                    LogError("Generic type {genericType} of {schemaName} not found", generic[i], fullName);
                    return null;
                }
                genericTypes[i] = type;
            }
            genericType = ActivatorUtilities.CreateInstance(Services, result.GetType()) as NodeType;
            if (genericType == null)
            {
                LogError("Generic type {schemaName} load failed", fullName);
                return null;
            }
            result.GenericMap ??= new ConcurrentDictionary<string, NodeType>();
            result.GenericMap[key] = genericType;
            await genericType.LoadTypeAsync(this, schema!.Clone(runtime), genericTypes);
            return genericType;

            #endregion
        }
        
        async Task<NodeSchema?> LoadNodeSchemaAsync(NamespaceType? @namespace, string name)
        {
            NodeSchema? schema = @namespace?.GetNodeSchema(name);
            if (schema != null && schema.Kind != SCHEMA_KIND_NAMESPACE) return schema;
            
            string schemaName = $"{@namespace?.Name}.{name}".Trim('.');
            schema = SetSchemaState(runtime.GetSystemSchema(schemaName), SchemaLoadState.System);
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
                    schema.CombineExtensions(loadSchema, runtime);

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
                            schema.Schemas[index].CombineExtensions(otherSchema, runtime);
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
                    //pass
                    LogError(e, $"Failed to load schema '{schemaName} from schema provider '{provider.GetType().FullName}'.");
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
    public async Task<T?> GetNodeTypeAsync<T>(string schemaName, GenericParameter[]? genericParameters = null, bool reload = false) where T : NodeType
        => await GetNodeTypeAsync(schemaName, genericParameters, reload) as T;
    
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
