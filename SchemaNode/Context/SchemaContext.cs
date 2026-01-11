using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchemaNode.Components;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Context;

/// <summary>
/// The schema context
/// </summary>
public class SchemaContext(IServiceProvider serviceProvider): IDisposable
{
    #region Static Settings

    /// <summary>
    /// The max take count for increment field query
    /// </summary>
    internal static readonly SchemaNodeConfig Config = new ();
    
    #endregion

    #region Init System Types

    /// <summary>
    /// Traverse all the defined system schemas, init without loading
    /// </summary>
    internal async Task InitSystemContextAsync()
    {
        SystemOnly = true;
        await GetSchemaTypeAsync("", preload: true);
        await GetAppTypeAsync("", preload: true);
        ResetTypeNamespace(RootNamespace);
        ResetAppType(RootAppType);
    }

    void ResetTypeNamespace(TypeNamespace root)
    {
        root.Loaded = false;
        foreach (TypeNamespace? ns in root.SchemaNodes.Values.Where(n => n is TypeNamespace).Cast<TypeNamespace>())
            ResetTypeNamespace(ns);
    }
    
    void ResetAppType(AppType root)
    {
        if (root.Fields is { Count: > 0 }) return;
        root.Loaded = false;
        if (root.SubAppList != null)
        {
            foreach (AppType? app in root.SubAppList.Values)
                ResetAppType(app);
        }
    }
    
    #endregion
    
    #region Properties
    
    /// <summary>
    /// The schema provider
    /// </summary>
    internal IServiceProvider ServiceProvider { get; } = serviceProvider;

    /// <summary>
    /// Gets the logger
    /// </summary>
    internal ILogger Logger => _loggerThunk.Value;
    
    /// <summary>
    /// For system only
    /// </summary>
    internal bool SystemOnly { get; set; }
    
    #endregion

    #region Services

    /// <summary>
    /// Gets the required service
    /// </summary>
    public T GetRequiredService<T>() where T: notnull => ServiceProvider.GetRequiredService<T>();
    
    /// <summary>
    /// Gets the required service
    /// </summary>
    public object GetRequiredService(Type serviceType) => ServiceProvider.GetRequiredService(serviceType);
    
    /// <summary>
    /// Gets the service
    /// </summary>
    public T? GetService<T>() where T: notnull => ServiceProvider.GetService<T>();
    
    /// <summary>
    /// Gets the service
    /// </summary>
    public object? GetService(Type serviceType) => ServiceProvider.GetService(serviceType);
    
    /// <summary>
    /// Gets the services
    /// </summary>
    public IEnumerable<T> GetServices<T>() where T: notnull => ServiceProvider.GetServices<T>();
    
    /// <summary>
    /// Gets the services
    /// </summary>
    public IEnumerable<object?> GetServices(Type serviceType) => ServiceProvider.GetServices(serviceType);

    #endregion

    #region Log

    public void LogDebug(string message, params object?[] args) => Logger.LogDebug(message, args);
    public void LogInformation(string message, params object?[] args) => Logger.LogInformation(message, args);
    public void LogWarning(string message, params object?[] args) => Logger.LogWarning(message, args);
    public void LogError(Exception ex, string message, params object?[] args) => Logger.LogError(ex, message, args);

    #endregion
    
    #region Schema Methods

    // Gets the schema type through namespace
    async Task<AnySchemaType?> GetSchemaTypeAsync(TypeNamespace node, string[] paths, bool reload = false, bool preload = false)
    {
        // Generic type check
        string[]? generic = null;
        string path = paths.Length > 0 ? paths[0] : string.Empty;
        if (paths.Length == 1 && Regex.IsMatch(path, REGEX_GENERIC_IMPLEMENT))
        {
            Match match = Regex.Match(path, REGEX_GENERIC_IMPLEMENT);
            path = match.Groups[1].Value;
            generic = match.Groups[2].Value.Split(",", StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).ToArray();
        }
        
        // Try gets the sub node
        AnySchemaType? subNode = paths.Length == 0 ? node : node.SchemaNodes.GetValueOrDefault(path);
        NodeSchema? nodeSchema = null;
        string schemaName = subNode == RootNamespace ? "" : node != RootNamespace ? $"{node.Name}.{path}" : path;
        
        // Init if not exist
        if (subNode == null)
        {
            Logger.LogInformation("[Runtime]Schema Type {schemaName} loading", schemaName);
            nodeSchema = await this.LoadSchemaAsync(schemaName, SystemOnly);
            if (nodeSchema == null) return null;
            subNode = InitSchemaType(node, nodeSchema);
        }
        
        // Reload or Load if is the access node
        if (paths.Length <= 1)
        {
            if (reload || !subNode.Loaded)
            {
                // Load the schema
                Logger.LogInformation("[Runtime]Schema Type {schemaName} loading", schemaName);

                // Avoid recycle load
                subNode.Loaded = true;
                
                // Re-load schema for full definition
                nodeSchema ??= await this.LoadSchemaAsync(schemaName, SystemOnly);
                if (nodeSchema == null)
                {
                    Logger.LogError("[Runtime]Schema Type {schemaName} load failed", schemaName);
                    return null;
                }

                // Load the node
                subNode.Display = nodeSchema.Display;
                subNode.Release();
                subNode.Auth = !string.IsNullOrWhiteSpace(nodeSchema.Auth)
                    ? await GetSchemaTypeAsync<PolicyType>(nodeSchema.Auth)
                    : null;
                subNode.Status = SchemaNodeStatus.Ready;

                await subNode.LoadAsync(this, nodeSchema, preload);

                Logger.LogInformation("[Runtime]Schema Type {schemaName} working", schemaName);
            }

            // Generic type handling
            return generic != null
                ? subNode switch
                    {
                        StructType @struct => await @struct.GetGenericTypeAsync(this, generic),
                        ArrayType array => await array.GetGenericTypeAsync(this, generic[0]),
                        _ => null
                    }
                : subNode;
        }

        return subNode is TypeNamespace subNs && paths.Length > 1
            ? await GetSchemaTypeAsync(subNs, paths.Skip(1).ToArray(), reload, preload)
            : null;
    }

    /// <summary>
    /// Gets the schema node
    /// </summary>
    public async Task<AnySchemaType?> GetSchemaTypeAsync(string schemaName, bool reload = false, bool preload = false)
        => string.IsNullOrWhiteSpace(schemaName) && RootNamespace.Loaded ? RootNamespace
            : Regex.IsMatch(schemaName, REGEX_GENERIC_TYPE) ? GenericType.Instance
                : await GetSchemaTypeAsync(RootNamespace, schemaName.ToLowerInvariant().SplitTypeName(), reload, preload);

    /// <summary>
    /// Gets the schema node of specific type
    /// </summary>
    public async Task<T?> GetSchemaTypeAsync<T>(string schemaName, bool reload = false, bool preload = false) where T : AnySchemaType
        => await GetSchemaTypeAsync(schemaName, reload, preload) as T;
    
    /// <summary>
    /// Remove a node from cache
    /// </summary>
    internal bool RemoveSchemaType(string schemaName)
    {
        AnySchemaType? node = RootNamespace;
        if (string.IsNullOrWhiteSpace(schemaName)) return false;
        
        // gets the node
        string[] paths = schemaName.SplitTypeName();
        foreach (string path in paths.SkipLast(1))
        {
            // Gets the sub node
            if (node is not TypeNamespace parent || !parent.SchemaNodes.TryGetValue(path, out node)) return false;
        }

        if (node is TypeNamespace ns)
        {
            if (ns.SchemaNodes.TryGetValue(paths.Last(), out AnySchemaType? child))
            {
                if (child.IsUsed) return false;
                ns.SchemaNodes.TryRemove(paths.Last(), out child);
                child?.Dispose();
            }
            ns.Schemas = ns.Schemas.Where(s => !s.Name.Equals(schemaName, StringComparison.OrdinalIgnoreCase)).ToArray();
            return true;
        }

        return false;
    }

    // Gets the app type through namespace
    async Task<AppType?> GetAppTypeAsync(AppType root, string[] paths, bool reload = false, bool preload = false)
    {
        Logger.LogInformation("Getting App Type: {root} - {paths}", root.Name, string.Join(".", paths));
        
        string path = paths.Length > 0 ? paths[0] : string.Empty;
        AppType? subApp = paths.Length > 0 ? root.SubAppList?.GetValueOrDefault(paths[0]) : root;
        string name = subApp == RootAppType ? string.Empty : root != RootAppType ? $"{root.Name}.{path}" : path;

        // Loading and init
        AppSchema? appSchema = null;
        if (subApp == null)
        {
            Logger.LogInformation("[Runtime]App Type {name} loading", name);
            appSchema = await this.LoadAppSchemaAsync(name, SystemOnly);
            if (appSchema == null) return null;
            subApp = InitAppType(root, appSchema);
        }
        
        // Reload or Load if is the access node
        if (paths.Length > 1) return await GetAppTypeAsync(subApp, paths.Skip(1).ToArray(), reload, preload);
        
        // Reload or Load
        if (reload || !subApp.Loaded)
        {
            // Avoid recycle load
            subApp.Loaded = true;
            
            appSchema ??= await this.LoadAppSchemaAsync(name, SystemOnly);
            if (appSchema == null) return null;
                
            await subApp.LoadAsync(this, appSchema, preload);
            Logger.LogInformation("[Runtime]App Type {name} working", name);
        }
        
        // Load sub app if preload
        else if (preload && subApp.Apps != null && (subApp.Apps.Length != subApp.SubAppList?.Count || subApp.SubAppList.Any(p => !p.Value.Loaded)))
        {
            // Load all the sub application list
            foreach (string n in subApp.Apps.Select(p => p.Name))
                await GetAppTypeAsync(n, preload: true);
        }

        return subApp;

    }
    
    /// <summary>
    /// Gets the category node
    /// </summary>
    public async Task<AppType?> GetAppTypeAsync(string name, bool reload = false, bool preload = false)
        => string.IsNullOrWhiteSpace(name) && RootAppType.Loaded ? RootAppType
            : await GetAppTypeAsync(RootAppType, Regex.Split(name.Trim().ToLowerInvariant(), @"\W+")
                .Where(s => !string.IsNullOrWhiteSpace(s)).ToArray(), reload, preload);

    /// <summary>
    /// Remove an app from cache
    /// </summary>
    internal bool RemoveAppType(string appName)
    {
        AppType? node = RootAppType;
        if (string.IsNullOrWhiteSpace(appName)) return false;

        // gets the node
        string[] paths = Regex.Split(appName.Trim().ToLowerInvariant(), @"\W+").Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
        foreach (string path in paths.SkipLast(1))
        {
            // Gets the sub node
            if (node.SubAppList == null || !node.SubAppList.TryGetValue(path, out node)) return false;
        }

        if (node.SubAppList is null) return false;

        if (node.SubAppList.TryGetValue(paths.Last(), out AppType? child))
        {
            if (child.IsUsed) return false;
            node.SubAppList.TryRemove(paths.Last(), out child);
        }
        node.Apps = node.Apps?.Where(s => !s.Name.Equals(appName, StringComparison.OrdinalIgnoreCase)).ToArray() ?? [];
        return true;
    }

    /// <summary>
    /// Convert the value to schema node
    /// </summary>
    public async Task<AnySchemaNode?> GetSchemaNodeAsync<T>(T? value)
    {
        if (value is null) return null;
        string? schemaType = typeof(T).GetSchemaType(true);
        return string.IsNullOrEmpty(schemaType) ? null : (await GetSchemaTypeAsync(schemaType))?.CreateNode(value);
    }

    /// <summary>
    /// Gets the array schema type
    /// </summary>
    public async Task<ArrayType?> GetArraySchemaTypeAsync(AnySchemaType? type)
    {
        if (type == null) return null;
        return type.GetArrayType()
               ?? await ((await GetSchemaTypeAsync(NS_SYSTEM_LIST)) as ArrayType)!
                   .GetGenericTypeAsync(this, type.Name);
    }
    
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
    public T? GetContextItem<T>() where T : class
    {
        return _contextItems.TryGetValue(typeof(T), out object? value) ? value as T : null;
    }
    
    /// <summary>
    /// Gets the context item
    /// </summary>
    public object? GetContextItem(Type type)
    {
        return _contextItems.TryGetValue(type, out object? value) ? value : null;
    }
    
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

    #region Dispose

    public void Dispose()
    {
        foreach (var item in _contextItems)
        {
            (item.Value as IDisposable)?.Dispose();
        }
    }

    #endregion

    #region Utility

    static AnySchemaType InitSchemaType(TypeNamespace root, NodeSchema schema)
    {
        AnySchemaType schemaType = schema!;
        root.SchemaNodes[schema.Name.SplitTypeName().Last()] = schemaType;
        schemaType.Namespace = root;
        
        if (Array.FindIndex(root.Schemas, s => s.Name.Equals(schemaType.Name, StringComparison.OrdinalIgnoreCase)) < 0)
            root.Schemas = root.Schemas.Append(schema).ToArray();
        
        switch (schemaType)
        {
            case TypeNamespace ns when schema.Schemas != null:
            {
                foreach (NodeSchema sub in schema.Schemas)
                    InitSchemaType(ns, sub);
                break;
            }
        }
        return schemaType;
    }

    static AppType InitAppType(AppType root, AppSchema schema)
    {
        AppType app = new AppType { Name = schema.Name, RootApp = root };
        root.SubAppList ??= new ConcurrentDictionary<string, AppType>();
        root.SubAppList[schema.Name.SplitTypeName().Last()] = app;
        if (root.Apps == null ||
            !root.Apps.Any(s => s.Name.Equals(schema.Name, StringComparison.OrdinalIgnoreCase)))
            root.Apps = root.Apps?.Append(schema).ToArray() ?? [schema];
        if (schema.Apps != null)
        {
            foreach (AppSchema sub in schema.Apps)
                InitAppType(app, sub);
        }
        return app;
    }

    readonly ConcurrentDictionary<Type, object> _contextItems = [];        
    readonly Lazy<ILogger> _loggerThunk = new(serviceProvider.GetRequiredService<ILogger<SchemaContext>>);
    
    internal static readonly TypeNamespace RootNamespace = new TypeNamespace { Name = "" };
    internal static readonly AppType RootAppType = new AppType { Name = "" };

    #endregion
}