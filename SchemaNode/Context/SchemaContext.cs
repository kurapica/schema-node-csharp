using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchemaNode.Components;
using SchemaNode.Components.Provider;
using SchemaNode.Enum;
using SchemaNode.Function;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.Schema;
using static SchemaNode.Utility.App;

namespace SchemaNode.Context;

/// <summary>
/// The schema context
/// </summary>
public class SchemaContext
{
    #region Constructor

    static SchemaContext()
    {
        RootNamespace = new TypeNamespace{ Name = "" };
        RootAppType = new AppType { Name = "" };
    }
    
    public SchemaContext(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
        _loggerThunk = new Lazy<ILogger>(serviceProvider.GetRequiredService<ILogger<SchemaContext>>);
        _dataProviderThunk = new Lazy<IAppSchemaDataProvider?>(serviceProvider.GetService<IAppSchemaDataProvider>);
        _criticalRegionProvider = new Lazy<ICriticalRegionProvider>(serviceProvider.GetRequiredService<ICriticalRegionProvider>);
    }
    
    #endregion
    
    #region Properties
    
    /// <summary>
    /// The schema provider
    /// </summary>
    internal IServiceProvider ServiceProvider { get; }
    
    /// <summary>
    /// Gets the logger
    /// </summary>
    internal ILogger Logger => _loggerThunk.Value;
    
    /// <summary>
    /// Gets the app data provider
    /// </summary>
    internal IAppSchemaDataProvider? AppDataProvider => _dataProviderThunk.Value;

    /// <summary>
    /// The current category target to be used
    /// </summary>
    internal string Target { get; set; } = string.Empty;

    #endregion

    #region Static Method

    /// <summary>
    /// Gets cached schema type
    /// </summary>
    internal static AnySchemeType? GetCachedSchemaType(string schemaName)
    {
        AnySchemeType? node = RootNamespace;
        string fullPath = "";
        foreach (string path in Regex.Split(schemaName.Trim().ToLowerInvariant(), @"\W+")
                     .Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            if (node is not TypeNamespace parent) return null;
            fullPath = !string.IsNullOrWhiteSpace(fullPath) ? $"{fullPath}.{path}" : path;

            // Gets the sub node
            if (parent.SchemaNodes.TryGetValue(path, out node)) continue;
            return null;
        }
        return node;
    }

    #endregion

    #region Schema Provider Apis

    /// <summary>
    /// Load the schema information
    /// </summary>
    /// <param name="schemaName">The schema name</param>
    /// <returns>The schema</returns>
    public async Task<NodeSchema?> LoadSchemaAsync(string schemaName)
    {
        NodeSchema? schema = GetSystemNodeSchema(schemaName);
        if (schema != null)
        {
            if (schema.Type != SchemaType.Namespace) return schema;
        }
        
        foreach (ISchemaProvider provider in ServiceProvider.GetServices<ISchemaProvider>())
        {
            try
            {
                NodeSchema[] loadSchemas = await provider.LoadSchemaAsync([schemaName]);
                if (loadSchemas.Length == 0) continue;
                NodeSchema loadSchema = loadSchemas[0];
                
                // load provider & state
                loadSchema.SchemaProvider = provider.GetType();
                if (loadSchema.LoadState == null && provider.DefaultLoadState != null)
                    loadSchema.LoadState = provider.DefaultLoadState;
                
                // check && combine
                if (schema == null)
                {
                    schema = loadSchema;
                }
                else if (loadSchema is { Type: SchemaType.Namespace, Schemas: not null } && loadSchema.Schemas.Length != 0)
                {
                    // combine
                    loadSchema.Schemas = schema.Schemas == null || schema.Schemas?.Length == 0
                        ? loadSchema.Schemas
                        :schema.Schemas!.Concat(loadSchema.Schemas.Where(s => !schema.Schemas!.Any(v => s.Name.Equals(v.Name, StringComparison.OrdinalIgnoreCase))).ToArray()).ToArray();
                    schema = loadSchema;
                }
                if (schema.Type != SchemaType.Namespace) return schema;
            }
            catch
            {
                //pass
            }
        }
        return schema;
    }

    /// <summary>
    /// Load the app schema information
    /// </summary>
    /// <param name="schemaName">The app schema name</param>
    /// <returns>The app schema</returns>
    public async Task<AppSchema?> LoadAppSchemaAsync(string schemaName)
    {
        AppSchema? schema = GetSystemApp(schemaName);
        if (schema?.Fields is { Length: > 0 }) return schema;

        foreach (ISchemaProvider provider in ServiceProvider.GetServices<ISchemaProvider>())
        {
            try
            {
                AppSchema? loadSchema = await provider.LoadAppSchemaAsync(schemaName);
                if (loadSchema == null) continue;

                // check && combine
                if (schema == null)
                {
                    schema = loadSchema;
                }
                else if ((schema.Fields == null || schema.Fields.Length == 0) && loadSchema.Apps is { Length: > 0 })
                {
                    // combine
                    schema.Apps = schema.Apps == null || schema.Apps?.Length == 0
                        ? loadSchema.Apps
                        : schema.Apps!.Concat(loadSchema.Apps.Where(s => !schema.Apps!.Any(v => s.Name.Equals(v.Name, StringComparison.OrdinalIgnoreCase))).ToArray()).ToArray();
                }
            }
            catch
            {
                //pass
            }
        }
        return schema;
    }

    /// <summary>
    /// Load the enum value sub list
    /// </summary>
    /// <param name="node">The enum schema node</param>
    /// <param name="value">The root enum value, optional</param>
    /// <param name="fullList">Whether load the full list</param>
    /// <returns></returns>
    public async Task<EnumValueInfo[]> LoadEnumSubListAsync(EnumType node, string? value, bool? fullList = null)
    {
        if (node.SchemaProvider != null)
        {
            return await ((ISchemaProvider)ServiceProvider.GetRequiredService(node.SchemaProvider)).LoadEnumSubListAsync(node.Name, value, fullList);
        }
        foreach (ISchemaProvider provider in ServiceProvider.GetServices<ISchemaProvider>())
        {
            try
            {
                EnumValueInfo[] result = await provider.LoadEnumSubListAsync(node.Name, value, fullList);
                node.SchemaProvider = provider.GetType();
                return result;
            }
            catch
            {
                //pass
            }
        }
        return [];
    }

    /// <summary>
    /// Load the enum value access list from the server
    /// </summary>
    /// <param name="node">The enum schema node</param>
    /// <param name="value">The enum value for access</param>
    /// <param name="noSubList">no sub list should be loaded</param>
    /// <param name="withSubList">with the value's sub list if existed</param>
    /// <returns></returns>
    public async Task<EnumValueAccess[]> LoadEnumAccessListAsync(EnumType node, string value, bool? noSubList = null, bool? withSubList = null)
    {
        if (node.SchemaProvider != null)
        {
            return await ((ISchemaProvider)ServiceProvider.GetRequiredService(node.SchemaProvider)).LoadEnumAccessListAsync(node.Name, value, noSubList, withSubList);
        }
        foreach (ISchemaProvider provider in ServiceProvider.GetServices<ISchemaProvider>())
        {
            try
            {
                EnumValueAccess[] result = await provider.LoadEnumAccessListAsync(node.Name, value, noSubList, withSubList);
                node.SchemaProvider = provider.GetType();
                return result;
            }
            catch
            {
                // pass
            }
        }
        return [];
    }

    /// <summary>
    /// Call the function with arguments and given generic type
    /// </summary>
    /// <param name="node">The function schema node</param>
    /// <param name="args">The arguments</param>
    /// <param name="generic">The generic types</param>
    /// <returns>The result</returns>
    public async Task<JsonNode?> CallFunctionAsync(FunctionType node, JsonArray args, string[]? generic = null)
    {
        if (node.IsRemoteCall)
        {
            return node.SchemaProvider != null
                ? await ((ISchemaProvider)ServiceProvider.GetRequiredService(node.SchemaProvider)).CallFunctionAsync(node.Name, args, generic)
                : null;
        }

        // Argument validation
        SchemaFuncInfo funcInfo = node.GetSchemaFuncInfo() ?? throw new Exception($"Function {node.Name} can't be complied");

        // fill generic if provided
        Type?[] generics = new Type?[funcInfo.Generics.Length];
        if (generic != null)
        {
            for (int i = 0; i < Math.Min(funcInfo.Generics.Length, generic.Length); i++)
            {
                if (string.IsNullOrEmpty(generic[i])) continue;
                AnySchemeType? ns = await GetSchemaNodeAsync(generic[i]);
                if (ns is { IsValueType: true }) generics[i] = ns.ToCSharpType();
            }
        }
        
        // parse parameters
        object?[] callArgs = new object[funcInfo.Args.Length];
        for(int i = 0; i < funcInfo.Args.Length; i++)
        {
            SchemaParamTypeInfo arg = funcInfo.Args[i];
            if (args.Count <= i || args[i] == null)
            {
                if (arg.Nullable) continue;
                throw new Exception($"The {i + 1} argument must be provided");
            }

            // generic type
            if (arg.Generic != null)
            {
                int idx = Array.FindIndex(funcInfo.Generics, f => f.Generic == arg.Generic);
                if (idx < 0) throw new Exception("The function not valid");
                
                (object? o, Type? _, Type? gen) = arg.ParseValue(args[i], generics[idx]);
                callArgs[i] = o ?? throw new Exception($"The {i+1} argument must be provided and valid");
                if (generics[idx] is null && gen is not null) generics[idx] = gen; // scan for generic
            }
            else if (arg.Type != null)
            {
                (object? o, Type? _, Type? _) = arg.ParseValue(args[i]);
                callArgs[i] = o ?? throw new Exception($"The {i + 1} argument must be provided and valid");
            }
            else
            {
                throw new Exception("The function not valid");
            }
        }

        if ((funcInfo.Sign & FUNC_SIGN_CONTEXT) > 0)
        {
            callArgs = callArgs.Prepend(this).ToArray();
        }
        
        
        // Call the method
        object? result;
        if ((funcInfo.Sign & FUNC_SIGN_IMMUTABLE) == FUNC_SIGN_IMMUTABLE)
        {
            MethodInfo callMethod = funcInfo.Method!;

            // Gets the generic method instance
            if ((funcInfo.Sign & FUNC_SIGN_GENERIC) == FUNC_SIGN_GENERIC)
            {
                for (int i = 0; i < generics.Length; i++)
                {
                    generics[i] ??= typeof(JsonNode);
                }
                if (generics.Any(g => g is null)) throw new Exception($"The generic types must be provided");
                
                string genSign = string.Join('|', generics.Select(p => p!.Name));
                callMethod = funcInfo.GenericMethods.GetOrAdd(genSign, _ => funcInfo.Method!.MakeGenericMethod(generics!));
            }

            // Call the method
            result = (funcInfo.Sign & FUNC_SIGN_ASYNC) == FUNC_SIGN_ASYNC
                ? GetCallAsyncFunc(callMethod.ReturnType.GetGenericArguments()[0]).Invoke(null, [callMethod, callArgs])
                : callMethod.Invoke(null, callArgs);
        }
        else
        {
            // Invoke the dynamic method
            try
            {
                result = funcInfo.DynamicMethod!.DynamicInvoke(callArgs);
            }
            catch (Exception ex)
            {
                while (ex.InnerException != null) ex = ex.InnerException;
                // ReSharper disable once PossibleIntendedRethrow
                throw ex;
            }
        }
        if (result != null)
        {
            return result switch
            {
                AnySchemaNode n => n.ToJson(),
                JsonObject obj => obj,
                JsonArray arr => arr,
                JsonValue val => val,
                _ => result.ToJsonNode()
            };
        }
        return null;
    }

    /// <summary>
    /// Call the function with arguments and given generic type
    /// </summary>
    /// <param name="name">The function schema name</param>
    /// <param name="args">The arguments</param>
    /// <param name="generic">The generic types</param>
    /// <returns>The result</returns>
    public async Task<JsonNode?> CallFunctionAsync(string name, JsonArray args, string[]? generic = null)
    {
        AnySchemeType? node = await GetSchemaNodeAsync(name);
        if (node is not FunctionType funcNode) throw new Exception($"Function {name} not found");
        return await CallFunctionAsync(funcNode, args, generic);
    }

    #endregion

    #region Schema Storage Apis

    /// <summary>
    /// Save the schema to the storage
    /// </summary>
    /// <param name="schema">The schema</param>
    /// <returns>true if saved</returns>
    public async Task<bool> SaveSchemaAsync(NodeSchema schema)
    {
        AnySchemeType? node = await GetSchemaNodeAsync(schema.Name);
        
        // save the schema
        ISchemaStorageProvider? provider = ServiceProvider.GetService<ISchemaStorageProvider>();
        if (provider == null) return false;
        if (!await provider.SaveSchemaAsync(schema)) return false;

        // save runtime
        if (node == null)
        {
            AnySchemeType? parentNode = await GetSchemaNodeAsync(string.Join('.', schema.Name.Split(".").Where(s => !string.IsNullOrEmpty(s)).SkipLast(1)));
            if (parentNode is TypeNamespace ns)
                ns.Schemas = ns.Schemas.Concat([schema]).ToArray();
        }
        await GetSchemaNodeAsync(schema.Name, reload: true); // force reload
        await this.PublishMessageAsync(new SchemaChangeMessage
        {
            Schemas = [schema.Name]
        });
        return true;
    }

    /// <summary>
    /// Delete the schema from the storage
    /// </summary>
    /// <param name="name">The schema</param>
    /// <returns>true if deleted</returns>
    public async Task<bool> DeleteSchemaAsync(string name)
    {
        AnySchemeType? node = await GetSchemaNodeAsync(name);
        if (node == null || node.IsUsed) return false;
        
        ISchemaStorageProvider? provider = ServiceProvider.GetService<ISchemaStorageProvider>();
        if (provider == null) return false;
        if (!await provider.DeleteSchemaAsync(name)) return false;

        RemoveSchemaNode(name);
        await this.PublishMessageAsync(new SchemaChangeMessage
        {
            DeleteSchemas = [name]
        });
        return true;
    }

    /// <summary>
    /// Save the sub list for an enum value
    /// </summary>
    /// <param name="name">The schema name</param>
    /// <param name="value">The enum value</param>
    /// <param name="values">The enum sub list</param>
    /// <param name="append">Whether append the sub list not replace</param>
    /// <returns>true if saved</returns>
    public async Task<bool> SaveEnumSubListAsync(string name, string? value, EnumValueInfo[] values, bool? append)
    {
        AnySchemeType? node = await GetSchemaNodeAsync(name);
        if (node is not EnumType @enum) return false;
        
        ISchemaStorageProvider? provider = ServiceProvider.GetService<ISchemaStorageProvider>();
        if (provider == null) return false;
        
        // save the sub list
        @enum.SaveEnumSubListAsync(value, await provider.SaveEnumSubListAsync(@enum, value, values, append));
        await this.PublishMessageAsync(new SchemaChangeMessage
        {
            Schemas = [name]
        });
        return true;
    }

    /// <summary>
    /// Save the app schema
    /// </summary>
    /// <param name="app"></param>
    /// <returns></returns>
    public async Task<bool> SaveAppSchemaAsync(AppSchema app)
    {
        AppType? node = await GetAppNodeAsync(app.Name);
        ISchemaStorageProvider? provider = ServiceProvider.GetService<ISchemaStorageProvider>();
        if (provider == null) return false;
        if (!await provider.SaveAppSchemaAsync(app)) return false;

        if (node == null)
        {
            AppType? parentNode = await GetAppNodeAsync(string.Join('.', app.Name.Split(".").Where(s => !string.IsNullOrEmpty(s)).SkipLast(1)));
            if (parentNode != null)
            {
                parentNode.Apps = parentNode.Apps == null ? [app] : parentNode.Apps.Concat([app]).ToArray();
            }
        }
        await GetAppNodeAsync(app.Name, reload: true); // force reload
        await this.PublishMessageAsync(new SchemaChangeMessage
        {
            Apps = [app.Name]
        });
        return true;
    }

    /// <summary>
    /// Delete an app schema
    /// </summary>
    /// <param name="app"></param>
    /// <returns></returns>
    public async Task<bool> DeleteAppSchemaAsync(string app)
    {
        AppType? node = await GetAppNodeAsync(app);
        if (node == null || node.IsUsed) return false;

        ISchemaStorageProvider? provider = ServiceProvider.GetService<ISchemaStorageProvider>();
        if (provider == null) return false;
        if (!await provider.DeleteAppSchemaAsync(app)) return false;
        RemoveAppNode(app);
        await this.PublishMessageAsync(new SchemaChangeMessage
        {
            DeleteApps = [app]
        });
        return true;
    }

    /// <summary>
    /// Save app field schema
    /// </summary>
    public async Task<bool> SaveAppFieldSchemAsync(string app, AppFieldSchema field)
    {
        AppType? node = await GetAppNodeAsync(app);
        if (node == null) return false;

        ISchemaStorageProvider? provider = ServiceProvider.GetService<ISchemaStorageProvider>();
        if (provider == null) return false;
        if (!await provider.SaveAppFieldSchemaAsync(app, field)) return false;

        await GetAppNodeAsync(app, reload: true);
        await this.PublishMessageAsync(new SchemaChangeMessage
        {
            Apps = [app]
        });
        return true;
    }

    /// <summary>
    /// Delete app field schema
    /// </summary>
    public async Task<bool> DeleteAppFieldSchemaAsync(string app, string field)
    {
        AppType? node = await GetAppNodeAsync(app);
        if (node == null) return false;

        ISchemaStorageProvider? provider = ServiceProvider.GetService<ISchemaStorageProvider>();
        if (provider == null) return false;
        if (!await provider.DeleteAppFieldSchemaAsync(app, field)) return false;

        await GetAppNodeAsync(app, reload: true);
        await this.PublishMessageAsync(new SchemaChangeMessage
        {
            Apps = [app]
        });
        return true;
    }

    /// <summary>
    /// Swap the field order
    /// </summary>
    /// <param name="app"></param>
    /// <param name="field1"></param>
    /// <param name="field2"></param>
    /// <returns></returns>
    public async Task<bool> SwapAppFieldSchemaAsync(string app, string field1, string field2)
    {
        AppType? node = await GetAppNodeAsync(app);
        if (node == null) return false;

        ISchemaStorageProvider? provider = ServiceProvider.GetService<ISchemaStorageProvider>();
        if (provider == null) return false;
        if (!await provider.SwapAppFieldSchemaAsync(app, field1, field2)) return false;

        await GetAppNodeAsync(app, reload: true);
        await this.PublishMessageAsync(new SchemaChangeMessage
        {
            Apps = [app]
        });
        return true;
    }

    #endregion

    #region Schema Methods

    /// <summary>
    /// Gets the schema node
    /// </summary>
    public async Task<AnySchemeType?> GetSchemaNodeAsync(string schemaName, bool reload = false, bool preload = false)
    {
        AnySchemeType? node = RootNamespace;
        
        // gets the node
        string fullPath = "";
        string[] paths = Regex.Split(schemaName.Trim().ToLowerInvariant(), @"\W+")
            .Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
        for (int i = 0; i < paths.Length - 1; i++)
        {
            if (node is not TypeNamespace parent) return null;
            string path = paths[i];
            fullPath = !string.IsNullOrWhiteSpace(fullPath) ? $"{fullPath}.{path}" : path;
            
            // Gets the sub node
            if (parent.SchemaNodes.TryGetValue(path, out node)) continue;
            
            // Must be a namespace
            node = new TypeNamespace { Name = fullPath };

            if (!parent.SchemaNodes.TryAdd(path, node))
                node = parent.SchemaNodes[path];
        }

        TypeNamespace? par = null;
        if (paths.Length > 0)
        {
            par = (TypeNamespace)node;
            node = par.SchemaNodes.GetValueOrDefault(paths.Last());
        }
        
        if (!reload && node is { Loaded: true }) return node;
        
        // reload the node
        Logger.LogInformation("[Runtime]Schema Type {schema} loading", schemaName);
        if (node != null) node.Loaded = true;
        NodeSchema? newSchema = await LoadSchemaAsync(schemaName);
        if (newSchema != null)
        {
            if (node == null)
            {
                node = newSchema;
                par!.SchemaNodes.TryAdd(paths.Last(), node!);
            }
            node!.Loaded = true;
            node.Display = newSchema.Display;
            node.Release();
            node.Status = SchemaNodeStatus.Ready;
            await node.LoadAsync(this, newSchema, preload);
        }
        Logger.LogInformation("[Runtime]Schema Type {schema} working", schemaName);
        return node;
    }

    /// <summary>
    /// Remove a node from cache
    /// </summary>
    internal bool RemoveSchemaNode(string schemaName)
    {
        AnySchemeType? node = RootNamespace;
        if (string.IsNullOrWhiteSpace(schemaName)) return false;
        
        // gets the node
        string[] paths = Regex.Split(schemaName.Trim().ToLowerInvariant(), @"\W+").Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
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
    public async Task<AppType?> GetAppNodeAsync(string name, bool reload = false, bool preload = false)
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

            node = new AppType { Name = fullPath };
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
        AppSchema? appSchema = await LoadAppSchemaAsync(name);
        if (appSchema == null) return node;
        
        if (node == null)
        {
            node = new AppType{ Name = name };
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
    /// <param name="appName"></param>
    /// <returns></returns>
    internal bool RemoveAppNode(string appName)
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

    #endregion

    #region Lock

    /// <summary>
    /// Lock by key
    /// </summary>
    public Task<ICriticalRegion> GetLockAsync(string lockKey, params object[] args)
        => _criticalRegionProvider.Value.AcquireAsync(string.Format(lockKey, args));

    /// <summary>
    /// Lock by key with timeout
    /// </summary>
    public Task<ICriticalRegion> GetLockAsync(string lockKey, TimeSpan timeout, params object[] args)
        => _criticalRegionProvider.Value.AcquireAsync(string.Format(lockKey, args), timeout);

    /// <summary>
    /// The critical region provider
    /// </summary>
    private readonly Lazy<ICriticalRegionProvider> _criticalRegionProvider;

    #endregion
    
    #region Dynamic Data

    #region Table Management

    /// <summary>
    /// Prepare the dynamic table for the field
    /// </summary>
    internal async Task<DynamicTableSchema> PrepareFieldDataAsync(AppFieldType field)
    {
        // no front only & enable & no source ref
        if (!field.EnableDynamicTable)
            return field.Schema ??= field.GenDynamicTableSchema();

        // Return the data
        DynamicTableSchema? schema = field.Schema;
        if (schema != null) return schema;

        if (AppDataProvider == null) throw new InvalidOperationException(APP_DATA_PROVIDER_NOT_EXIST);
        using ICriticalRegion locker = await GetLockAsync($"SCHEMA_CONTEXT_DYN_TABLE_CREATION:{field.DynamicTableName}");
        try
        {
            schema = field.Schema;
            if (schema != null) return schema;

            schema = field.GenDynamicTableSchema();
            await AppDataProvider.EnsureDynamicTableAsync(schema);
            field.Schema = schema;
            return schema;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"PrepareFieldDataAsync {field.DynamicTableName} Error");
            throw;
        }
    }

    /// <summary>
    /// Prepare the dynamic table for the field
    /// </summary>
    public async Task<List<DynamicTableSchema>> PrepareFieldDataAsync(AppType type)
    {
        List<DynamicTableSchema> schemaList = new();
        if (type.Fields == null) return schemaList;
        
        // prepare the fields
        foreach (AppFieldType field in type.Fields)
            schemaList.Add(await PrepareFieldDataAsync(field));

        // prepare the ref field
        if (type.RefField != null)
            await PrepareFieldDataAsync(type.RefField);
        return schemaList;
    }

    #endregion

    #region App Source Field
 
    /// <summary>
    /// Sets the ref target of the field
    /// </summary>
    public async Task<bool> SetSourceFieldNode(AppFieldType field, string target, string sourceTarget)
    {
        if (field.SourceNode == null) return false;
        AppType? category = await GetAppNodeAsync(field.App);
        if (category?.RefField == null) return false;
        JsonObject data = new()
        {
            { APP_FIELD_REF_APP, field.SourceApp },
            { APP_FIELD_REF_TARGET, sourceTarget }
        };
        return await SaveFieldDataAsync(category.RefField, target, data);
    }

    /// <summary>
    /// Sets the ref target of the field
    /// </summary>
    public async Task<bool> SetSourceFieldNode(AppType app, string target, string sourceApp, string sourceTarget)
    {
        AppFieldType? field = app.Fields?.FirstOrDefault(f => sourceApp.Equals(f.SourceApp, StringComparison.OrdinalIgnoreCase));
        return field == null || await SetSourceFieldNode(field, target, sourceTarget);
    }

    /// <summary>
    /// Sets the ref target of the field
    /// </summary>
    public async Task<bool> SetSourceFieldNode(string app, string target, string sourceApp, string sourceTarget)
    {
        AppType? node = await GetAppNodeAsync(app);
        return node == null || await SetSourceFieldNode(node, target, sourceApp, sourceTarget);
    }

    /// <summary>
    /// Gets the source field node
    /// </summary>
    public async Task<(AppFieldType?, string)> GetSourceFieldNode(AppFieldType? field, string target, bool forPush = false)
    {
        if (field?.SourceNode == null) return (field, target);
        AppType? category = await GetAppNodeAsync(field.App);

        // Means the category is front only and use the source node's target as target
        if (category?.RefField == null) return forPush ? (null, string.Empty) : await GetSourceFieldNode(field.SourceNode, target);

        JsonObject query = new() { { APP_FIELD_REF_APP, field.SourceNode.App } };
        (AnySchemaNode? refData, _) = await GetFieldDataAsync(category.RefField, target, query);
        if (refData is ArrayTypeNode { Count: > 0 } arr && arr[0] is StructTypeNode jObject && jObject[APP_FIELD_REF_TARGET] is ScalarTypeNode val && !val.IsEmpty)
        {
            string? reftarget = val.ToValue<string>();
            if (!string.IsNullOrWhiteSpace(reftarget))
            {
                return await GetSourceFieldNode(field.SourceNode, reftarget, forPush);
            }
        }

        // Consider use the same target if no ref for view
        return forPush ? (null, string.Empty) : await GetSourceFieldNode(field.SourceNode, target);
    }

    #endregion
    
    #region Data Management

    /// <summary>
    /// Save entity data
    /// </summary>
    public async Task<bool> SaveEntityAsync<T>(string target, T value)
    {
        (string app, string field, PropertyInfo[] _)? app = typeof(T).GetSystemAppField();
        if (app == null) throw new ArgumentException($"The type {typeof(T).FullName} is not a valid app field data type");

        AppType? appNode = await GetAppNodeAsync(app.Value.app);
        AppFieldType appFieldNode = appNode?.GetField(app.Value.field) ?? throw new ArgumentException($"The type {typeof(T).FullName} is not a valid app field data type");

        return await SaveFieldDataAsync(appFieldNode, target, appFieldNode.TypeNode!.CreateNode(value));
    }

    /// <summary>
    /// Save entity list data
    /// </summary>
    public async Task<bool> SaveEntitysAsync<T>(string target, List<T> values)
    {
        (string app, string field, PropertyInfo[] _)? app = typeof(T).GetSystemAppField();
        if (app == null) throw new ArgumentException($"The type {typeof(T).FullName} is not a valid app field data type");

        AppType? appNode = await GetAppNodeAsync(app.Value.app);
        AppFieldType? appFieldNode = appNode?.GetField(app.Value.field) ?? throw new ArgumentException($"The type {typeof(T).FullName} is not a valid app field data type");

        return await SaveFieldDataAsync(appFieldNode, target, appFieldNode.TypeNode!.CreateNode(values));
    }

    /// <summary>
    /// Sve field data
    /// </summary>
    public Task<bool> SaveFieldDataAsync(AppFieldType field, string target, JsonNode? value = null)
    {
        AnySchemaNode data = field.TypeNode!.CreateNode(value) ?? throw new NotSupportedException();
        return SaveFieldDataAsync(field, target, data);
    }

    /// <summary>
    /// Save the field data by data
    /// </summary>
    public async Task<bool> SaveFieldDataAsync(AppFieldType field, string target, AnySchemaNode? value = null, bool innerCall = false)
    {
        // no front only & enable & no source ref
        if (!field.EnableDynamicTable) return false;

        // Not allow the direct data update
        if (!innerCall && !string.IsNullOrWhiteSpace(field.Func)) return false;
        if (AppDataProvider == null) throw new InvalidOperationException(APP_DATA_PROVIDER_NOT_EXIST);
        
        // Prepare
        DynamicTableSchema schema = await PrepareFieldDataAsync(field);

        try
        {
            (bool result, AnySchemaNode? origin) = await AppDataProvider.SaveDynamicTableDataAsync(schema, target, value);
            if (result) OnFieldDataChanged(target, field, TransactionChangeOperation.Modify, value, origin);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Delete entity data
    /// </summary>
    public async Task DeleteEntityAsync<T>(string target, T value)
    {
        (string app, string field, PropertyInfo[] primarys)? app = typeof(T).GetSystemAppField();
        if (app == null) throw new ArgumentException($"The type {typeof(T).FullName} is not a valid app field data type");

        AppType? appNode = await GetAppNodeAsync(app.Value.app);
        AppFieldType appFieldNode = appNode?.GetField(app.Value.field) ?? throw new ArgumentException($"The type {typeof(T).FullName} is not a valid app field data type");

        JsonObject query = [];
        foreach(PropertyInfo prop in app.Value.primarys)
        {
            query[prop.Name] = JsonValue.Create(prop.GetValue(value) ?? throw new ArgumentException($"The primary key {prop.Name} value is null"));
        }

        await DeleteFieldListDataAsync(appFieldNode, target, [query]);
    }

    /// <summary>
    /// Delete entity data
    /// </summary>
    public async Task DeleteEntityAsync<T>(string target, params object[] primarys)
    {
        (string app, string field, PropertyInfo[] primarys)? app = typeof(T).GetSystemAppField();
        if (app == null) throw new ArgumentException($"The type {typeof(T).FullName} is not a valid app field data type");

        AppType? appNode = await GetAppNodeAsync(app.Value.app);
        AppFieldType appFieldNode = appNode?.GetField(app.Value.field) ?? throw new ArgumentException($"The type {typeof(T).FullName} is not a valid app field data type");

        if (primarys.Length != app.Value.primarys.Length) throw new ArgumentException($"The type {typeof(T).FullName} primary key count not match");

        JsonObject query = [];
        for(int i = 0; i < primarys.Length; i++)
        {
            query[app.Value.primarys[i].Name.ToCamelCase()] = JsonValue.Create(primarys[i]);
        }

        await DeleteFieldListDataAsync(appFieldNode, target, [query]);
    }


    /// <summary>
    /// Delete entity data
    /// </summary>
    public async Task DeleteEntitysAsync<T>(string target, params (string key, object value)[] keys)
    {
        if (keys.Length == 0) throw new ArgumentException("At least one key must be provided");

        (string app, string field, PropertyInfo[] primarys)? app = typeof(T).GetSystemAppField();
        if (app == null) throw new ArgumentException($"The type {typeof(T).FullName} is not a valid app field data type");

        AppType? appNode = await GetAppNodeAsync(app.Value.app);
        AppFieldType? appFieldNode = appNode?.GetField(app.Value.field) ?? throw new ArgumentException($"The type {typeof(T).FullName} is not a valid app field data type");

        JsonObject query = [];
        for (int i = 0; i < keys.Length; i++)
        {
            query[keys[i].key.ToCamelCase()] = JsonValue.Create(keys[i].value);
        }

        await DeleteFieldListDataAsync(appFieldNode, target, [query]);
    }

    /// <summary>
    /// Delete entity data
    /// </summary>
    public async Task DeleteEntitysAsync<T>(string target, List<T> value)
    {
        (string app, string field, PropertyInfo[] primarys)? app = typeof(T).GetSystemAppField();
        if (app == null) throw new ArgumentException($"The type {typeof(T).FullName} is not a valid app field data type");

        AppType? appNode = await GetAppNodeAsync(app.Value.app);
        AppFieldType appFieldNode = appNode?.GetField(app.Value.field) ?? throw new ArgumentException($"The type {typeof(T).FullName} is not a valid app field data type");

        JsonArray querys = [];
        foreach (T valueItem in value)
        {
            JsonObject query = [];
            foreach (PropertyInfo prop in app.Value.primarys)
            {
                query[prop.Name] = JsonValue.Create(prop.GetValue(valueItem) ?? throw new ArgumentException($"The primary key {prop.Name} value is null"));
            }
            querys.Add(query);
        }

        await DeleteFieldListDataAsync(appFieldNode, target, querys);
    }

    /// <summary>
    /// Delete the list from a list-struct type field data
    /// </summary>
    public async Task DeleteFieldListDataAsync(AppFieldType field, string target, JsonArray query, bool innerCall = false)
    {
        // no front only & enable & no source ref
        if (!field.EnableDynamicTable) return;
        if (AppDataProvider == null) throw new InvalidOperationException(APP_DATA_PROVIDER_NOT_EXIST);
        
        // Prepare
        DynamicTableSchema schema = await PrepareFieldDataAsync(field);

        // Only non-single schema can be used
        if (schema.Single) return;
        try
        {
            (bool result, AnySchemaNode? origin) = await AppDataProvider.DeleteDynamicTableDataAsync(schema, target, query);
            if (result)
                OnFieldDataChanged(target, field, TransactionChangeOperation.Delete, null, origin);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Delete the target's field data
    /// </summary>
    public async Task DeleteFieldDataAsync(AppFieldType field, string target, bool innerCall = false)
    {
        // no front only & enable & no source ref
        if (!field.EnableDynamicTable) return;
        if (AppDataProvider == null) throw new InvalidOperationException(APP_DATA_PROVIDER_NOT_EXIST);
        
        // Prepare
        DynamicTableSchema schema = await PrepareFieldDataAsync(field);
        
        try
        {
            (bool result, AnySchemaNode? origin) = await AppDataProvider.DeleteDynamicTableDataAsync(schema, target);
            if (result)
                OnFieldDataChanged(target, field, 
                    schema.Single ? TransactionChangeOperation.Delete : TransactionChangeOperation.DropAll, 
                    null, origin);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Gets the entity data by full primary keys
    /// </summary>
    public async Task<T?> GetEntityAsync<T>(string target, params object[] primarys)
    {
        (string app, string field, PropertyInfo[] primarys)? app = typeof(T).GetSystemAppField();
        if (app == null) throw new ArgumentException($"The type {typeof(T).FullName} is not a valid app field data type");
        if (primarys.Length != app.Value.primarys.Length) throw new ArgumentException($"The type {typeof(T).FullName} primary key count not match");

        JsonObject query = [];
        for (int i = 0; i < primarys.Length; i++)
        {
            query[app.Value.primarys[i].Name.ToCamelCase()] = JsonValue.Create(primarys[i]);
        }

        (List<T> result, _) = await GetFieldDataAsync<T>(target, query, take: 1);
        return result is { Count: > 0 } ? result[0] : default;
    }

    /// <summary>
    /// Gets the entity data by primary keys
    /// </summary>
    public async Task<List<T>> GetEntitysAsync<T>(string target, params (string key, object value)[] keys)
    {
        JsonObject query = [];
        for (int i = 0; i < keys.Length; i++)
        {
            query[keys[i].key.ToCamelCase()] = JsonValue.Create(keys[i].value);
        }

        (List<T> result, _) = await GetFieldDataAsync<T>(target, query);
        return result;
    }

    /// <summary>
    /// Gets the entity data
    /// </summary>
    public async Task<(List<T> value, int total)> GetFieldDataAsync<T>(string target, T? filter = default, int skip = 0, int take = 0, bool desc = false, AppSchemaDataOrder[]? orderBy = null)
    {
        (string app, string field, PropertyInfo[] primarys)? app = typeof(T).GetSystemAppField();
        if (app == null) throw new ArgumentException($"The type {typeof(T).FullName} is not a valid app field data type");

        AppType? appNode = await GetAppNodeAsync(app.Value.app);
        AppFieldType appFieldNode = appNode?.GetField(app.Value.field) ?? throw new ArgumentException($"The type {typeof(T).FullName} is not a valid app field data type");

        JsonObject? query = null;
        if (filter != null)
        {
            query = [];
            foreach(PropertyInfo prop in app.Value.primarys)
            {
                object? val = prop.GetValue(filter);
                if (val != null) query[prop.Name] = JsonValue.Create(val);
            }
        }

        (AnySchemaNode? result, int total) = await GetFieldDataAsync(appFieldNode, target, query, skip, take, desc, orderBy);
        List<T> results = [];
        if (result is ArrayTypeNode arr)
        {
            foreach (AnySchemaNode item in arr)
            {
                if (item is StructTypeNode obj)
                {
                    T? val = obj.ToValue<T>();
                    if (val != null) results.Add(val);
                }
            }
        }
        else if (result is StructTypeNode obj)
        {
            T? val = obj.ToValue<T>();
            if (val != null) results.Add(val);
        }
        return (results, total);
    }


    public async Task<(List<T> value, int total)> GetFieldDataAsync<T>(string target, List<T> filter, int skip = 0, int take = 0, bool desc = false, AppSchemaDataOrder[]? orderBy = null)
    {
        (string app, string field, PropertyInfo[] primarys)? app = typeof(T).GetSystemAppField();
        if (app == null) throw new ArgumentException($"The type {typeof(T).FullName} is not a valid app field data type");

        AppType? appNode = await GetAppNodeAsync(app.Value.app);
        AppFieldType appFieldNode = appNode?.GetField(app.Value.field) ?? throw new ArgumentException($"The type {typeof(T).FullName} is not a valid app field data type");

        JsonArray querys = [];
        foreach (T valueItem in filter)
        {
            JsonObject query = [];
            foreach (PropertyInfo prop in app.Value.primarys)
            {
                query[prop.Name] = JsonValue.Create(prop.GetValue(valueItem) ?? throw new ArgumentException($"The primary key {prop.Name} value is null"));
            }
            querys.Add(query);
        }

        (AnySchemaNode? result, int total) = await GetFieldDataAsync(appFieldNode, target, querys, skip, take, desc, orderBy);
        List<T> results = [];
        if (result is ArrayTypeNode arr)
        {
            foreach (AnySchemaNode item in arr)
            {
                if (item is StructTypeNode obj)
                {
                    T? val = obj.ToValue<T>();
                    if (val != null) results.Add(val);
                }
            }
        }
        else if (result is StructTypeNode obj)
        {
            T? val = obj.ToValue<T>();
            if (val != null) results.Add(val);
        }
        return (results, total);
    }

    /// <summary>
    /// Gets the entity data
    /// </summary>
    public async Task<(List<T> value, int total)> GetFieldDataAsync<T>(string target, JsonNode filter, int skip = 0, int take = 0, bool desc = false, AppSchemaDataOrder[]? orderBy = null)
    {
        (string app, string field, PropertyInfo[] primarys)? app = typeof(T).GetSystemAppField();
        if (app == null) throw new ArgumentException($"The type {typeof(T).FullName} is not a valid app field data type");

        AppType? appNode = await GetAppNodeAsync(app.Value.app);
        AppFieldType appFieldNode = appNode?.GetField(app.Value.field) ?? throw new ArgumentException($"The type {typeof(T).FullName} is not a valid app field data type");


        (AnySchemaNode? result, int total) = await GetFieldDataAsync(appFieldNode, target, filter, skip, take, desc, orderBy);
        List<T> results = [];
        if (result is ArrayTypeNode arr)
        {
            foreach (AnySchemaNode item in arr)
            {
                if (item is StructTypeNode obj)
                {
                    T? val = obj.ToValue<T>();
                    if (val != null) results.Add(val);
                }
            }
        }
        else if (result is StructTypeNode obj)
        {
            T? val = obj.ToValue<T>();
            if (val != null) results.Add(val);
        }
        return (results, total);
    }

    /// <summary>
    /// Gets the field data
    /// </summary>
    public async Task<(AnySchemaNode? value, int total)> GetFieldDataAsync(AppFieldType? field, string target, JsonNode? filter = null, int skip = 0, int take = 0, bool desc = false, AppSchemaDataOrder[]? orderBy = null)
    {
        // Front end only
        if ((field?.Frontend ?? false) || (field?.Disable ?? false)) return (null, 0);
        if (AppDataProvider == null) throw new InvalidOperationException(APP_DATA_PROVIDER_NOT_EXIST);

        (field, target) = await GetSourceFieldNode(field, target);
        if (field == null) return (null, 0);

        DynamicTableSchema schema = await PrepareFieldDataAsync(field);

        string original = Target;
        try
        {
            Target = target;
            
            (AnySchemaNode? result, int total) = await AppDataProvider.QueryDynamicTableAsync(schema, target, filter, skip, take, desc, orderBy);
            
            // Generate display only fields
            await schema.GenerateDisplayOnlyFields(this, result);
            
            return (result, total);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.Message);
            throw;
        }
        finally
        {
            Target = original;
        }
    }

    #endregion

    #region Transaction

    /// <summary>
    /// Begin transaction.
    /// </summary>
    public async Task BeginTransactionAsync()
    {
        if (AppDataProvider == null) throw new InvalidOperationException(APP_DATA_PROVIDER_NOT_EXIST);
        await AppDataProvider.BeginTransactionAsync();
        _transChangedData.Clear();
    }

    /// <summary>
    /// Commit transaction.
    /// </summary>
    public async Task CommitTransactionAsync(bool pushAll = false, bool pushAllFields = false)
    {
        if (AppDataProvider == null) throw new InvalidOperationException(APP_DATA_PROVIDER_NOT_EXIST);
        
        // Process data field push
        foreach (string target in _transChangedData.Keys.ToArray())
        {
            // process data push
            await ProcessDataPush(target, _transChangedData[target], pushAll, pushAllFields);
        }

        await AppDataProvider.CommitTransactionAsync();
    }

    /// <summary>
    /// Rollback transaction.
    /// </summary>
    public async Task RollbackTransactionAsync()
    {
        if (AppDataProvider == null) throw new InvalidOperationException(APP_DATA_PROVIDER_NOT_EXIST);
        await AppDataProvider.RollbackTransactionAsync();
        _transChangedData.Clear();
    }

    // Process the data push
    async Task ProcessDataPush(string target, TransactionChangeData changeData, bool pushAll = false, bool pushAllFields = false, AppFieldType? pushNode = null)
    {
        // record the target
        Target = target;

        // Build the push generation
        List<AppFieldType> baseFields = changeData.Changes.Keys.Where(p => p.Observers is { Count: > 0 }).ToList();

        // If push all
        if (pushAllFields)
        {
            baseFields.Clear();
            foreach (string app in changeData.Changes.Keys.Select(p => p.App).Distinct())
            {
                AppType? appNode = await GetAppNodeAsync(app);
                if (appNode?.Fields != null)
                {
                    baseFields.AddRange(appNode.Fields.Where(f => f.FuncNode == null && f.Observers is { Count: > 0 }));
                }
            }
        }

        // Generate the push levels
        FieldDataPushLevel? root = null;
        FieldDataPushLevel? curr = null;
        Dictionary<string, FieldDataPushLevel> updateFieldsLvlMap = new();

        // The given push node
        if (pushNode != null)
        {
            root = new FieldDataPushLevel
            {
                Fields =
                {
                    pushNode
                }
            };
            curr = root;
            if (baseFields.Count == 0 && pushNode.Observers is { Count: > 0 })
                baseFields = root.Fields;
        }
        while (baseFields.Count > 0)
        {
            FieldDataPushLevel next = new();

            // Check fields
            foreach (AppFieldType node in baseFields.Where(p => p.Observers != null).SelectMany(p => p.Observers!).Distinct().Where(n => !(n.Disable ?? false) && !(n.Frontend ?? false)))
            {
                if (!updateFieldsLvlMap.ContainsKey(node.Name))
                {
                    next.Fields.Add(node);
                    updateFieldsLvlMap.Add(node.Name, next);
                }
                else
                {
                    // Move the field to current
                    AppFieldType item = updateFieldsLvlMap[node.Name].Fields.First(p => p.Name == node.Name);
                    next.Fields.Add(item);
                    updateFieldsLvlMap[node.Name].Fields.Remove(item);
                    updateFieldsLvlMap[node.Name] = next;
                }
            }

            // Link the levels
            if (next.Fields.Count > 0)
            {
                if (curr != null)
                {
                    curr.Next = next;
                }
                else
                {
                    root = next;
                }
                curr = next;
            }
            else
            {
                break;
            }
            baseFields = next.Fields.Where(p => p.Observers is { Count: > 0 }).ToList();
        }

        // Process data push
        Dictionary<AppFieldType, AnySchemaNode> otherFields = new();
        HashSet<AppFieldType> displayOnlyGens = [];
        HashSet<string> otherTargets = [];
        while (root?.Fields.Count is > 0)
        {
            foreach (AppFieldType field in root.Fields)
            {
                // Check ref
                AppFieldType? tarField = field;
                string realTarget = target;
                if (field.SourceNode != null)
                {
                    (tarField, realTarget) = await GetSourceFieldNode(field, target, true);
                    if (tarField == null) continue;
                    if (realTarget != target) otherTargets.Add(realTarget);
                }

                // Prepare arguments
                FunctionType? funcNode = field.FuncNode;
                if (funcNode == null || field.FuncArgs == null) continue;
                FieldDataPushArg[] args = new FieldDataPushArg[field.FuncArgs.Count];
                int arrayIndex = -1;
                for (int i = 0; i < field.FuncArgs.Count; i++)
                {
                    AppFieldNodeArgument call = field.FuncArgs[i];
                    args[i] = new FieldDataPushArg();

                    // Generate argument
                    List<FieldDataChangeData>? changes = (!pushAll || field.SourceNode != null) && changeData.Changes.TryGetValue(call.AppField, out List<FieldDataChangeData>? dataChange) ? dataChange : null;
                    args[i].Type = call.AppField.TypeNode!;
                    if (args[i].Type is ArrayType && (funcNode.Args[i].TypeNode is not ArrayType || arrayIndex < 0)) arrayIndex = i;

                    // Check changes
                    if (changes == null)
                    {
                        args[i].IsFull = true;
                        args[i].Changed = false;

                        // full data
                        if (otherFields.ContainsKey(call.AppField))
                        {
                            args[i].Value = otherFields[call.AppField].IsEmpty ? null : otherFields[call.AppField];
                        }
                        else
                        {
                            (args[i].Value, _) = await GetFieldDataAsync(call.AppField, target);
                            otherFields[call.AppField] = args[i].Value ?? call.AppField.TypeNode!.CreateNode()!;
                        }
                        args[i].Origin = args[i].Value;
                    }
                    else
                    {
                        // generate display only fields for upload data
                        if (displayOnlyGens.Add(call.AppField))
                        {
                            // check schema
                            if (call.AppField.TypeNode is ArrayType { ElementNode: StructType } or StructType)
                            {
                                DynamicTableSchema schema = await PrepareFieldDataAsync(call.AppField);
                                foreach (FieldDataChangeData change in changes)
                                {
                                    // for new
                                    if (change.Value is ArrayTypeNode vArr)
                                    {
                                        foreach (var token in vArr)
                                        {
                                            if (token is StructTypeNode { IsEmpty: false } obj)
                                            {
                                                await schema.GenerateDisplayOnlyFields(this, obj);
                                            }
                                        }
                                    }
                                    else if (change.Value is StructTypeNode { IsEmpty: false } vObj)
                                    {
                                        await schema.GenerateDisplayOnlyFields(this, vObj);
                                    }

                                    // for origin
                                    if (change.Origin is ArrayTypeNode oarr)
                                    {
                                        foreach (var token in oarr)
                                        {
                                            if (token is StructTypeNode { IsEmpty: false } obj)
                                            {
                                                await schema.GenerateDisplayOnlyFields(this, obj);
                                            }
                                        }
                                    }
                                    else if (change.Origin is StructTypeNode { IsEmpty: false } gObj)
                                    {
                                        await schema.GenerateDisplayOnlyFields(this, gObj);
                                    }
                                }
                            }
                        }

                        args[i].Changed = true;
                        if (call.AppField.TypeNode is ArrayType @array)
                        {
                            // Check array if need part update
                            ArrayTypeNode values = new(@array);
                            ArrayTypeNode origins = new(@array);
                            foreach (FieldDataChangeData change in changes)
                            {
                                switch (change.Operation)
                                {
                                    case TransactionChangeOperation.Create:
                                        if (change.Value is { IsEmpty: false })
                                        {
                                            if (change.Value is ArrayTypeNode vArr)
                                            {
                                                //  For array without primary keys
                                                args[i].IsFull = true;
                                                values.AddRange(vArr);
                                            }
                                            else
                                            {
                                                values.Add(change.Value);
                                            }
                                        }
                                        break;
                                    case TransactionChangeOperation.Modify:
                                        if (change.Value is { IsEmpty: false })
                                        {
                                            if (change.Value is ArrayTypeNode vArr)
                                            {
                                                //  For array without primary keys
                                                args[i].IsFull = true;
                                                values.AddRange(vArr);
                                            }
                                            else
                                            {
                                                values.Add(change.Value);
                                            }
                                        }
                                        if (change.Origin is { IsEmpty: false })
                                        {
                                            if (change.Origin is ArrayTypeNode vArr)
                                            {
                                                //  For array without primary keys
                                                args[i].IsFull = true;
                                                origins.AddRange(vArr);
                                            }
                                            else
                                            {
                                                origins.Add(change.Origin);
                                            }
                                        }
                                        break;
                                    case TransactionChangeOperation.Delete:
                                        if (change.Origin is { IsEmpty: false })
                                        {
                                            if (change.Origin is ArrayTypeNode vArr)
                                            {
                                                //  For array without primary keys
                                                args[i].IsFull = true;
                                                origins.AddRange(vArr);
                                            }
                                            else
                                            {
                                                origins.Add(change.Origin);
                                            }
                                        }
                                        break;
                                    case TransactionChangeOperation.DropAll:
                                        args[i].IsFull = true;
                                        if (change.Origin is ArrayTypeNode arr)
                                            origins.AddRange(arr);
                                        break;
                                    default:
                                        throw new ArgumentOutOfRangeException();
                                }
                            }
                            args[i].Value = values;
                            args[i].Origin = origins;
                        }
                        else
                        {
                            args[i].IsFull = true;
                            foreach (FieldDataChangeData change in changes)
                            {
                                switch (change.Operation)
                                {
                                    case TransactionChangeOperation.Create:
                                        args[i].Value = change.Value;
                                        break;
                                    case TransactionChangeOperation.Modify:
                                        args[i].Value = change.Value;
                                        args[i].Origin = change.Origin;
                                        break;
                                    case TransactionChangeOperation.Delete:
                                        args[i].Origin = change.Origin;
                                        break;
                                    case TransactionChangeOperation.DropAll:
                                        args[i].Origin = change.Origin;
                                        break;
                                    default:
                                        throw new ArgumentOutOfRangeException();
                                }
                            }
                        }
                    }

                    // Check data field
                    if (!string.IsNullOrWhiteSpace(call.DataField))
                    {
                        if (args[i].Type is StructType)
                        {
                            // Gets the value
                            args[i].Value = ((StructTypeNode?)args[i].Value)?.GetValueByPaths(call.DataField);

                            // Gets the origin
                            args[i].Origin = ((StructTypeNode?)args[i].Origin)?.GetValueByPaths(call.DataField);
                        }
                        else if (args[i].Type is ArrayType { ElementNode: StructType })
                        {
                            // Gets the value
                            if (args[i].Value is ArrayTypeNode arr)
                            {
                                for (int h = 0; h < arr.Count; h++)
                                {
                                    arr[h] = ((StructTypeNode?)arr[h])?.GetValueByPaths(call.DataField);
                                }
                            }

                            // Gets the origin
                            if (args[i].Origin is ArrayTypeNode oarr)
                            {
                                for (int h = 0; h < oarr.Count; h++)
                                {
                                    oarr[h] = ((StructTypeNode?)oarr[h])?.GetValueByPaths(call.DataField);
                                }
                            }
                        }
                    }
                }

                // Check if there are changed field beyond part update field, need full update
                // So normally simple field'll contains the settings that won't be upgraded, but if changes all should be rebuilt
                if (field.SourceNode == null && args.Any(p => p is { Changed: true, IsArray: false }) && arrayIndex >= 0 && !args[arrayIndex].IsFull)
                {
                    FieldDataPushArg arg = args[arrayIndex];
                    AppFieldNodeArgument call = field.FuncArgs[arrayIndex];

                    // full data
                    if (otherFields.ContainsKey(call.AppField))
                    {
                        arg.Value = otherFields[call.AppField].IsEmpty ? null : otherFields[call.AppField];
                    }
                    else
                    {
                        (arg.Value, _) = await GetFieldDataAsync(call.AppField, target);
                        otherFields[call.AppField] = arg.Value ?? call.AppField.TypeNode!.CreateNode()!;
                    }
                    arg.Origin = arg.Value;
                    arg.IsFull = true;
                }

                // If part update or is ref, must get the original calc result
                AnySchemaNode? oldResult = null;
                if (arrayIndex >= 0) // && (!args[arrayIndex].IsFull || field.SourceNode != null))
                {
                    JsonArray originCall = new();
                    foreach (FieldDataPushArg arg in args)
                        originCall.Add(arg.Origin?.ToJson());

                    // Check if use element
                    if (funcNode.Args[arrayIndex].TypeNode is not ArrayType)
                    {
                        JsonArray resultArr = new();
                        if (args[arrayIndex].Origin is ArrayTypeNode origin)
                        {
                            foreach (AnySchemaNode t in origin)
                            {
                                originCall[arrayIndex] = t.ToJson();
                                JsonNode? calcRes = await CallFunctionAsync(field.FuncNode!, originCall);
                                if (calcRes is JsonArray arr)
                                {
                                    foreach (JsonNode? ele in arr)
                                    {
                                        if (!ele.IsEmpty())
                                            resultArr.Add(ele!.DeepClone());
                                    }
                                }
                                else if (!calcRes.IsEmpty())
                                {
                                    resultArr.Add(calcRes!.DeepClone());
                                }
                            }
                        }

                        oldResult = new ArrayTypeNode(field.TypeNode!, resultArr);
                    }
                    else
                    {
                        JsonNode? r = await CallFunctionAsync(field.FuncNode!, originCall);
                        oldResult = r is JsonArray arr ? new ArrayTypeNode(field.TypeNode!, arr) : field.TypeNode!.CreateNode(r);
                    }
                }

                // Calc the new result
                AnySchemaNode? newResult;
                JsonArray callArgs = new();
                foreach (FieldDataPushArg arg in args)
                    callArgs.Add(arg.Value?.ToJson());

                // Check if use element
                if (arrayIndex >= 0 && funcNode.Args[arrayIndex].TypeNode is not ArrayType)
                {
                    JsonArray resultArr = new();
                    if (args[arrayIndex].Value is ArrayTypeNode origin)
                    {
                        foreach (AnySchemaNode t in origin)
                        {
                            callArgs[arrayIndex] = t.ToJson();
                            JsonNode? calcRes = await CallFunctionAsync(field.FuncNode!, callArgs);
                            if (calcRes is JsonArray arr)
                            {
                                foreach (JsonNode? ele in arr)
                                {
                                    if (!ele.IsEmpty())
                                        resultArr.Add(ele!.DeepClone());
                                }
                            }
                            else if (!calcRes.IsEmpty())
                            {
                                resultArr.Add(calcRes!.DeepClone());
                            }
                        }
                    }

                    newResult = new ArrayTypeNode(field.TypeNode!, resultArr);
                }
                else
                {
                    JsonNode? r = await CallFunctionAsync(field.FuncNode!, callArgs);
                    newResult = r is JsonArray arr ? new ArrayTypeNode(field.TypeNode!, arr) : field.TypeNode!.CreateNode(r);
                }

                // Join the result
                AnySchemaNode? result = null;
                switch (field.TypeNode)
                {
                    case EnumType:
                        {
                            DataCombineType method = field.Combine ?? DataCombineType.Assign;
                            (AnySchemaNode? origin, _) = await GetFieldDataAsync(tarField, realTarget);
                            AnySchemaNode? now = GroupJoin(newResult, method);

                            // Update with join method
                            switch (method)
                            {
                                case DataCombineType.Assign:
                                    {
                                        result = now is { IsEmpty: false } ? now : origin;
                                        break;
                                    }
                                case DataCombineType.Init:
                                    {
                                        result = origin is { IsEmpty: false } ? origin : now;
                                        break;
                                    }
                            }
                            break;
                        }
                    case ScalarType scalar:
                        {
                            // Gets the join method
                            DataCombineType method = field.Combine ?? (scalar.IsNumber ? DataCombineType.Sum : DataCombineType.Assign);
                            
                            // Part
                            (AnySchemaNode? origin, _) = await GetFieldDataAsync(tarField, realTarget);
                            AnySchemaNode? old = GroupJoin(scalar, oldResult, method);
                            AnySchemaNode? now = GroupJoin(scalar, newResult, method);

                            // Update with join method
                            switch (method)
                            {
                                case DataCombineType.Assign:
                                    {
                                        result = now;
                                        break;
                                    }
                                case DataCombineType.Init:
                                    {
                                        result = origin is { IsEmpty: false } ? origin : now;
                                        break;
                                    }
                                case DataCombineType.Sum:
                                case DataCombineType.Count:
                                    {
                                        result = field.TypeNode.CreateNode(
                                            (origin is { IsEmpty: false } ? origin.ToValue<decimal>() : 0m) +
                                            (now is { IsEmpty: false } ? now.ToValue<decimal>() : 0m) -
                                            (old is { IsEmpty: false } ? old.ToValue<decimal>() : 0m)
                                        );
                                    }
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }
                            break;
                        }
                    case StructType { Fields.Length: > 0 } @struct:
                        {
                            // Gets the join method map
                            Dictionary<string, DataCombineType> joinMethodMap = new();

                            // Default join
                            foreach (StructFieldConfig f in @struct.Fields)
                            {
                                if (f.TypeNode is ScalarType s)
                                    joinMethodMap[f.Name] = field.Combines?.FirstOrDefault(o => o.Field.Equals(f.Name, StringComparison.OrdinalIgnoreCase))?.Type 
                                        ?? (s.IsNumber ? DataCombineType.Sum : DataCombineType.Assign);
                            }

                            // Gets the result
                            (AnySchemaNode? origin, _) = await GetFieldDataAsync(tarField, realTarget);
                            AnySchemaNode? old = GroupJoin(@struct, oldResult, joinMethodMap);
                            AnySchemaNode? now = GroupJoin(@struct, newResult, joinMethodMap);

                            // Update with join method
                            if ((origin == null || origin.IsEmpty) && (old == null || old.IsEmpty))
                            {
                                result = now;
                            }
                            else
                            {
                                StructTypeNode final = new StructTypeNode(@struct);
                                foreach (StructFieldConfig nodeField in @struct.Fields)
                                {
                                    AnySchemaNode? originFld = origin is StructTypeNode os ? os.GetField(nodeField.Name) : null;
                                    AnySchemaNode? oldFld = old is StructTypeNode ols ? ols.GetField(nodeField.Name) : null;
                                    AnySchemaNode? nowFld = now is StructTypeNode ns ? ns.GetField(nodeField.Name) : null;

                                    switch (joinMethodMap.GetValueOrDefault(nodeField.Name, DataCombineType.Assign))
                                    {
                                        case DataCombineType.Assign:
                                            {
                                                final[field.Name] = nowFld is { IsEmpty: false } ? nowFld : originFld;
                                                break;
                                            }
                                        case DataCombineType.Init:
                                            {
                                                final[nodeField.Name] = originFld is { IsEmpty: false } ? originFld : nowFld;
                                                break;
                                            }
                                        case DataCombineType.Sum when nodeField.TypeNode is ScalarType { IsNumber: true }:
                                        case DataCombineType.Count when nodeField.TypeNode is ScalarType { IsNumber: true }:
                                            {
                                                final[nodeField.Name] = nodeField.TypeNode.CreateNode(
                                                    (originFld is { IsEmpty: false } ? originFld.ToValue<decimal>() : 0m) +
                                                    (nowFld is { IsEmpty: false } ? nowFld.ToValue<decimal>() : 0m) -
                                                    (oldFld is { IsEmpty: false } ? oldFld.ToValue<decimal>() : 0m)
                                                );
                                                break;
                                            }
                                        default:
                                            throw new ArgumentOutOfRangeException();
                                    }
                                }
                                result = final;
                            }
                        
                            break;
                        }
                    case ArrayType { ElementNode: EnumType or ScalarType }:
                        {
                            result = newResult;
                            break;
                        }
                    case ArrayType { ElementNode: StructType { Fields: { Length: > 0 } } structNode, Primary: { Length: > 0 } } array:
                        {
                            // Gets the join method map
                            Dictionary<string, DataCombineType> joinMethodMap = new();

                            // Gets the value fields
                            List<string> valueFields = new();
                            Dictionary<string, AnySchemeType> primaryNodes = new();
                            foreach (StructFieldConfig fieldType in structNode.Fields)
                            {
                                if (!array.Primary.Contains(fieldType.Name))
                                {
                                    valueFields.Add(fieldType.Name);

                                    if (fieldType.TypeNode is ScalarType s)
                                    {
                                        joinMethodMap[fieldType.Name] = s.IsNumber ? DataCombineType.Sum : DataCombineType.Assign;
                                    }
                                }
                                else
                                    primaryNodes.Add(fieldType.Name, fieldType.TypeNode!);
                            }

                            // Based on array join methods
                            if (array.Combines != null)
                            {
                                foreach (DataCombine combine in array.Combines)
                                {
                                    joinMethodMap[combine.Field] = combine.Type;
                                }
                            }
                            // Based on field join methods
                            if (field.Combines != null)
                            {
                                foreach (DataCombine combine in field.Combines)
                                {
                                    joinMethodMap[combine.Field] = combine.Type;
                                }
                            }

                            // Generate result map
                            // Group join the old & now data
                            Dictionary<string, StructTypeNode> oldMap = GroupJoinObjectMap(array, oldResult, joinMethodMap);
                            Dictionary<string, StructTypeNode> nowMap = GroupJoinObjectMap(array, newResult, joinMethodMap);

                            // Query the original data
                            HashSet<string> keys = new();
                            JsonArray query = new();
                            foreach ((string key, StructTypeNode obj) in oldMap)
                            {
                                if (!keys.Add(key)) continue;
                                query.Add(obj.ToJson());
                            }
                            foreach ((string key, StructTypeNode obj) in nowMap)
                            {
                                if (!keys.Add(key)) continue;
                                query.Add(obj.ToJson());
                            }

                            // Gets the original data
                            Dictionary<string, StructTypeNode> resultMap = new Dictionary<string, StructTypeNode>();
                            if (!query.IsEmpty())
                            {
                                (AnySchemaNode? value, _) = await GetFieldDataAsync(tarField, realTarget, query);
                                if (value is ArrayTypeNode arr)
                                {
                                    foreach (var token in arr)
                                    {
                                        if (token is not StructTypeNode obj) continue;
                                        string? key = array.GetPrimaryKey(obj);
                                        if (string.IsNullOrWhiteSpace(key)) continue;
                                        resultMap[key] = obj;
                                    }
                                }
                            }

                            // Generate the result map
                            foreach (string key in keys)
                            {
                                if (resultMap.TryGetValue(key, out var res1))
                                {
                                    oldMap.TryGetValue(key, out StructTypeNode? old);
                                    nowMap.TryGetValue(key, out StructTypeNode? now);
                                    foreach (string s in valueFields)
                                    {
                                        AnySchemaNode? originFld = res1.GetField(s);
                                        AnySchemaNode? oldFld = old?.GetField(s);
                                        AnySchemaNode? nowFld = now?.GetField(s);

                                        switch (joinMethodMap.GetValueOrDefault(s, DataCombineType.Assign))
                                        {
                                            case DataCombineType.Assign:
                                                if (nowFld is { IsEmpty: false })
                                                    res1[s] = nowFld;
                                                break;
                                            case DataCombineType.Init:
                                                if (originFld == null || originFld.IsEmpty)
                                                    res1[s] = nowFld;
                                                break;
                                            case DataCombineType.Sum:
                                            case DataCombineType.Count:
                                                res1[s] = (originFld is { IsEmpty: false } ? originFld.ToValue<decimal>() : 0m) +
                                                    (nowFld is { IsEmpty: false } ? nowFld.ToValue<decimal>() : 0m) -
                                                    (oldFld is { IsEmpty: false } ? oldFld.ToValue<decimal>() : 0m);
                                                break;
                                            default:
                                                throw new ArgumentOutOfRangeException();
                                        }
                                    }
                                }
                                else if (nowMap.TryGetValue(key, out StructTypeNode? res))
                                {
                                    resultMap.Add(key, res);
                                    if (!oldMap.TryGetValue(key, out StructTypeNode? old)) continue;

                                    // Shouldn't be but still handle it
                                    foreach (string s in valueFields)
                                    {
                                        AnySchemaNode? oldFld = old?.GetField(s);
                                        AnySchemaNode? nowFld = res?.GetField(s);

                                        switch (joinMethodMap.GetValueOrDefault(s, DataCombineType.Assign))
                                        {
                                            case DataCombineType.Assign:
                                                if (nowFld == null || nowFld.IsEmpty)
                                                    res![s] = oldFld;
                                                break;
                                            case DataCombineType.Init:
                                                if (oldFld is { IsEmpty: false })
                                                    res![s] = oldFld;
                                                break;
                                            case DataCombineType.Sum:
                                            case DataCombineType.Count:
                                                res![s] = (nowFld is { IsEmpty: false } ? nowFld.ToValue<decimal>() : 0m) -
                                                    (oldFld is { IsEmpty: false } ? oldFld.ToValue<decimal>() : 0m);
                                                break;
                                            default:
                                                throw new ArgumentOutOfRangeException();
                                        }
                                    }
                                }
                            }
                            
                            // Convert the map to list, sorted by primary keys
                            List<StructTypeNode> joinObjs = resultMap.Values.ToList();
                            joinObjs.Sort((a, b) =>
                            {
                                foreach (string s in array.Primary)
                                {
                                    switch (primaryNodes[s])
                                    {
                                        case ScalarType { IsDate: true }:
                                            {
                                                DateTime ad = a.GetField(s)!.ToValue<DateTime>();
                                                DateTime bd = b.GetField(s)!.ToValue<DateTime>();
                                                if (!ad.Equal(bd))
                                                    return ad.LessThan(bd) ? -1 : 1;
                                                break;
                                            }
                                        case ScalarType { IsNumber: true }:
                                            {
                                                decimal ad = a.GetField(s)!.ToValue<decimal>();
                                                decimal bd = b.GetField(s)!.ToValue<decimal>();
                                                if (ad != bd)
                                                    return ad < bd ? -1 : 1;
                                                break;
                                            }
                                        default:
                                            {
                                                string ad = a[s]?.ToString() ?? "";
                                                string bd = b[s]?.ToString() ?? "";
                                                if (!ad.Equals(bd))
                                                    return string.Compare(ad, bd, StringComparison.OrdinalIgnoreCase);
                                                break;
                                            }
                                    }
                                }
                                return 0;
                            });

                            // Save to result
                            result = field.TypeNode.CreateNode(joinObjs);
                            break;
                        }
                }

                // Save
                await SaveFieldDataAsync(tarField, realTarget, result, true);
            }

            // Process next level
            root = root.Next;
        }

        // Process other targets
        foreach (string tar in otherTargets)
        {
            if (_transChangedData.TryGetValue(tar, out TransactionChangeData? val))
                await ProcessDataPush(tar, val);
        }
    }

    // Record the changed fields with changed values
    void OnFieldDataChanged(string target, AppFieldType field, TransactionChangeOperation operation, AnySchemaNode? value = null, AnySchemaNode? origin = null)
    {
        if (!_transChangedData.TryGetValue(target, out TransactionChangeData? changeData))
        {
            changeData = new TransactionChangeData();
            _transChangedData.Add(target, changeData);
        }
        if (changeData.Changes.TryGetValue(field, out List<FieldDataChangeData>? changes))
        {
            changes.Add(new FieldDataChangeData(operation, value, origin));
        }
        else
        {
            changeData.Changes.Add(field, [new FieldDataChangeData(operation, value, origin)]);
        }
    }

    #endregion

    #endregion

    #region Utility

    // should be sync, no concurrent required
    readonly Dictionary<string, TransactionChangeData> _transChangedData = new();
    
    // Call async function
    static T? CallAsyncFunc<T>(MethodBase asyncCall, params object[] callArgs)
    {
        Task<T>? task = (Task<T>?)asyncCall.Invoke(null, callArgs);
        return task == null ? default : task.GetAwaiter().GetResult();
    }

    // Gets the call async method
    static MethodInfo GetCallAsyncFunc(Type t) => CallAsyncMethodMap.GetOrAdd(t, p => typeof(SchemaContext).GetMethod(nameof(CallAsyncFunc), BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(p));


    private readonly Lazy<ILogger> _loggerThunk;
    private readonly Lazy<IAppSchemaDataProvider?> _dataProviderThunk;
    
    static readonly ConcurrentDictionary<Type, MethodInfo> CallAsyncMethodMap = new();
    static readonly TypeNamespace RootNamespace;
    static readonly AppType RootAppType;

    #endregion
}