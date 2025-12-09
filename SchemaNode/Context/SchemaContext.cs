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
    
    #region Properties
    
    /// <summary>
    /// The schema provider
    /// </summary>
    internal IServiceProvider ServiceProvider { get; } = serviceProvider;

    /// <summary>
    /// Gets the logger
    /// </summary>
    internal ILogger Logger => _loggerThunk.Value;
    
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

    public void LogDebug(string message) => Logger.LogDebug(message);
    public void LogInformation(string message) => Logger.LogInformation(message);
    public void LogWarning(string message) => Logger.LogWarning(message);
    public void LogError(string message) => Logger.LogError(message);

    #endregion
    
    #region Schema Methods

    /// <summary>
    /// Gets the schema node
    /// </summary>
    public async Task<AnySchemeType?> GetSchemaTypeAsync(string schemaName, bool reload = false, bool preload = false)
    {
        AnySchemeType? node = RootNamespace;

        // generic type holder, types with generic parameters won't be used directly
        if (Regex.IsMatch(schemaName, REGEX_GENERIC_TYPE)) 
            return GenericType.Instance;
        
        // gets the node
        string fullPath = "";
        string[] paths = schemaName.SplitTypeName();
        for (int i = 0; i < paths.Length - 1; i++)
        {
            if (node is not TypeNamespace parent) return null;
            string path = paths[i];
            fullPath = !string.IsNullOrWhiteSpace(fullPath) ? $"{fullPath}.{path}" : path;
            
            // Gets the sub node
            if (parent.SchemaNodes.TryGetValue(path, out node)) continue;
            
            // Must be a namespace
            node = new TypeNamespace { Name = fullPath, Namespace = parent };

            if (!parent.SchemaNodes.TryAdd(path, node))
                node = parent.SchemaNodes[path];
        }

        TypeNamespace? par = null;
        if (paths.Length > 0)
        {
            par = (TypeNamespace)node;
            node = par.SchemaNodes.GetValueOrDefault(paths.Last());

            // check if generic implementation
            if (node == null && Regex.IsMatch(paths.Last(), REGEX_GENERIC_IMPLEMENT))
            {
                var match = Regex.Match(paths.Last(), REGEX_GENERIC_IMPLEMENT);
                AnySchemeType? type = await GetSchemaTypeAsync(string.Join('.', paths.SkipLast(1).Append(match.Groups[1].Value)));
                string[] generic = match.Groups[2].Value.Split(",", StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim()).ToArray();
                return type switch
                {
                    StructType @struct => await @struct.GetGenericTypeAsync(this, generic),
                    ArrayType array => await array.GetGenericTypeAsync(this, generic[0]),
                    _ => null
                };
            }
        }
        
        if (!reload && node is { Loaded: true }) return node;
        
        // reload the node
        Logger.LogInformation("[Runtime]Schema Type {schema} loading", schemaName);
        if (node != null) node.Loaded = true;
        NodeSchema? newSchema = await this.LoadSchemaAsync(schemaName);
        if (newSchema != null)
        {
            if (node == null)
            {
                node = newSchema;
                par!.SchemaNodes.TryAdd(paths.Last(), node!);
            }
            node!.Loaded = true;
            node.Display = newSchema.Display;
            node.Namespace = par;
            node.Release();
            node.Auth = !string.IsNullOrEmpty(newSchema.Auth)
                ? await GetSchemaTypeAsync(newSchema.Auth) as PolicyType
                : null;
            node.Status = SchemaNodeStatus.Ready;
            await node.LoadAsync(this, newSchema, preload);
            if (par != null)
            {
                int index = -1;
                for (int i = 0; i < par.Schemas.Length; i++)
                {
                    if (par.Schemas[i].Name.Equals(schemaName, StringComparison.OrdinalIgnoreCase))
                    {
                        index = i;
                        par.Schemas[i] = newSchema;
                        break;
                    }
                }
                if (index < 0)
                {
                    par.Schemas = par.Schemas.Append(newSchema).ToArray();
                }
            }
        }
        Logger.LogInformation("[Runtime]Schema Type {schema} working", schemaName);
        return node;
    }

    /// <summary>
    /// Remove a node from cache
    /// </summary>
    internal bool RemoveSchemaType(string schemaName)
    {
        AnySchemeType? node = RootNamespace;
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
            if (ns.SchemaNodes.TryGetValue(paths.Last(), out AnySchemeType? child))
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
    
    /// <summary>
    /// Gets the category node
    /// </summary>
    public async Task<AppType?> GetAppTypeAsync(string name, bool reload = false, bool preload = false)
    {
        // From root
        AppType? node = RootAppType;
        name = name.ToLowerInvariant();

        // Gets the node
        string fullPath = string.Empty;
        string[] paths = Regex.Split(name, @"\W+").Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
        for (int i = 0; i < paths.Length - 1; i++)
        {
            AppType parent = node;
            string path = paths[i];
            fullPath = !string.IsNullOrWhiteSpace(fullPath) ? $"{fullPath}.{path}" : path;
            if (parent.SubAppList != null && parent.SubAppList.TryGetValue(path, out node)) continue;

            node = new AppType { Name = fullPath, RootApp = parent };
            parent.SubAppList ??= new ConcurrentDictionary<string, AppType>();
            if (!parent.SubAppList.TryAdd(path, node))
                node = parent.SubAppList[path];
        }

        AppType? par = null;
        if (paths.Length > 0)
        {
            par = node;
            node = par.SubAppList?.GetValueOrDefault(paths.Last());
        }
        
        if (!reload && node is { Loaded: true}) return node;

        // reload the node
        Logger.LogInformation("[Runtime]App Type {AppName} loading", name);
        if (node != null) node.Loaded = true;
        AppSchema? appSchema = await this.LoadAppSchemaAsync(name);
        if (appSchema == null) return node;
        
        if (node == null)
        {
            node = new AppType{ Name = name, RootApp = par };
            par!.SubAppList ??= new ConcurrentDictionary<string, AppType>();
            par.SubAppList.TryAdd(paths.Last(), node);
        }
        node.Loaded = true;
        await node.LoadAsync(this, appSchema, preload);
        Logger.LogInformation("[Runtime]App Type {AppName} working", name);
        return node;
    }

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
        if (string.IsNullOrEmpty(schemaType)) return null;
        return (await GetSchemaTypeAsync(schemaType))?.CreateNode(value);
    }

    /// <summary>
    /// Gets the array schema type
    /// </summary>
    public async Task<AnySchemeType?> GetArraySchemaTypeAsync(AnySchemeType? type)
    {
        if (type == null) return null;
        return type.GetArrayType()
               ?? await ((await GetSchemaTypeAsync(NS_SYSTEM_LIST)) as ArrayType)!.GetGenericTypeAsync(this, type.Name);
    }
    
    /// <summary>
    /// Gets the array schema type
    /// </summary>
    public async Task<AnySchemeType?> GetArraySchemaTypeAsync(string? name)
    {
        AnySchemeType? type = !string.IsNullOrEmpty(name) ? await GetSchemaTypeAsync(name) : null;
        return type switch
        {
            null => null,
            ArrayType arrayType => arrayType,
            _ => type.GetArrayType() ??
                 await ((await GetSchemaTypeAsync(NS_SYSTEM_LIST)) as ArrayType)!.GetGenericTypeAsync(this, type.Name)
        };
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

    readonly ConcurrentDictionary<Type, object> _contextItems = [];        
    readonly Lazy<ILogger> _loggerThunk = new(serviceProvider.GetRequiredService<ILogger<SchemaContext>>);
    
    static internal readonly TypeNamespace RootNamespace = new TypeNamespace { Name = "" };
    static internal readonly AppType RootAppType = new AppType { Name = "" };

    #endregion
}