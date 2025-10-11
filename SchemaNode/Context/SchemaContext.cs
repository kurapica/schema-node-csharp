using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchemaNode.Components;
using SchemaNode.Components.Provider;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Schema;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SchemaNode.Utility;
using static SchemaNode.Utility.Schema;
using static SchemaNode.Utility.Constant;
using SchemaNode.Function;

namespace SchemaNode.Context;

/// <summary>
/// The schema context
/// </summary>
public class SchemaContext
{
    #region Constructor

    static SchemaContext()
    {
        RootNamespace = new NamespaceNode{ Name = "" };
        RootAppNode = new AppNode { Name = "" };
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
    public IServiceProvider ServiceProvider { get; }
    
    /// <summary>
    /// Gets the logger
    /// </summary>
    public ILogger Logger => _loggerThunk.Value;
    
    /// <summary>
    /// Gets the app data provider
    /// </summary>
    protected IAppSchemaDataProvider? AppDataProvider => _dataProviderThunk.Value;

    /// <summary>
    /// The current category target to be used
    /// </summary>
    public string Target { get; set; } = string.Empty;

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
                loadSchema.SchemaProvider = provider;
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
        AppSchema? schema = null;

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
    public async Task<EnumValueInfo[]> LoadEnumSubListAsync(EnumNode node, string? value, bool? fullList = null)
    {
        if (node.SchemaProvider != null)
        {
            return await node.SchemaProvider.LoadEnumSubListAsync(node.Name, value, fullList);
        }
        foreach (ISchemaProvider provider in ServiceProvider.GetServices<ISchemaProvider>())
        {
            try
            {
                EnumValueInfo[] result = await provider.LoadEnumSubListAsync(node.Name, value, fullList);
                node.SchemaProvider = provider;
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
    public async Task<EnumValueAccess[]> LoadEnumAccessListAsync(EnumNode node, string value, bool? noSubList = null, bool? withSubList = null)
    {
        if (node.SchemaProvider != null)
        {
            return await node.SchemaProvider.LoadEnumAccessListAsync(node.Name, value, noSubList, withSubList);
        }
        foreach (ISchemaProvider provider in ServiceProvider.GetServices<ISchemaProvider>())
        {
            try
            {
                EnumValueAccess[] result = await provider.LoadEnumAccessListAsync(node.Name, value, noSubList, withSubList);
                node.SchemaProvider = provider;
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
    public async Task<JsonNode?> CallFunctionAsync(FunctionNode node, JsonArray args, string[]? generic = null)
    {
        if (node.IsRemoteCall)
        {
            return node.SchemaProvider != null
                ? await node.SchemaProvider.CallFunctionAsync(node.Name, args, generic)
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
                AnySchemaNode? ns = await GetSchemaNodeAsync(generic[i]);
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
                JsonObject obj => obj,
                JsonArray arr => arr,
                JsonValue val => val,
                _ => JsonValue.Create(result)
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
        AnySchemaNode? node = await GetSchemaNodeAsync(name);
        if (node is not FunctionNode funcNode) throw new Exception($"Function {name} not found");
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
        AnySchemaNode? node = await GetSchemaNodeAsync(schema.Name);
        ISchemaStorageProvider? provider = ServiceProvider.GetService<ISchemaStorageProvider>();
        if (provider == null) return false;
        if (!await provider.SaveSchemaAsync(schema)) return false;

        if (node == null)
        {
            AnySchemaNode? parentNode = await GetSchemaNodeAsync(string.Join('.', schema.Name.Split(".").Where(s => !string.IsNullOrEmpty(s)).SkipLast(1)));
            if (parentNode is NamespaceNode ns)
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
        AnySchemaNode? node = await GetSchemaNodeAsync(name);
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
        AnySchemaNode? node = await GetSchemaNodeAsync(name);
        if (node is not EnumNode @enum) return false;
        
        ISchemaStorageProvider? provider = ServiceProvider.GetService<ISchemaStorageProvider>();
        if (provider == null) return false;
        bool res = await provider.SaveEnumSubListAsync(@enum, value, values, append);
        if (res)
        {
            @enum.SaveEnumSubListAsync(value, values);
            await this.PublishMessageAsync(new SchemaChangeMessage
            {
                Schemas = [name]
            });
        }
        return res;
    }

    /// <summary>
    /// Delete the sub list for an enum value
    /// </summary>
    /// <param name="name">The schema name</param>
    /// <param name="value">The enum value</param>
    /// <returns>true if deleted</returns>
    public async Task<bool> DeleteEnumSubListAsync(string name, string value)
    {
        AnySchemaNode? node = await GetSchemaNodeAsync(name);
        if (node is not EnumNode @enum) return false;
        
        ISchemaStorageProvider? provider = ServiceProvider.GetService<ISchemaStorageProvider>();
        if (provider == null) return false;
        bool res = await provider.DeleteEnumSubListAsync(@enum, value);
        if (res)
        {
            @enum.DeleteEnumSubListAsync(value);
            await this.PublishMessageAsync(new SchemaChangeMessage
            {
                Schemas = [name]
            });
        }
        return res;
    }

    /// <summary>
    /// Save the app schema
    /// </summary>
    /// <param name="app"></param>
    /// <returns></returns>
    public async Task<bool> SaveAppSchemaAsync(AppSchema app)
    {
        AppNode? node = await GetAppNodeAsync(app.Name);
        ISchemaStorageProvider? provider = ServiceProvider.GetService<ISchemaStorageProvider>();
        if (provider == null) return false;
        if (!await provider.SaveAppSchemaAsync(app)) return false;

        if (node == null)
        {
            AppNode? parentNode = await GetAppNodeAsync(string.Join('.', app.Name.Split(".").Where(s => !string.IsNullOrEmpty(s)).SkipLast(1)));
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
        AppNode? node = await GetAppNodeAsync(app);
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
        AppNode? node = await GetAppNodeAsync(app);
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
        AppNode? node = await GetAppNodeAsync(app);
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
        AppNode? node = await GetAppNodeAsync(app);
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
    public async Task<AnySchemaNode?> GetSchemaNodeAsync(string schemaName, bool reload = false, bool preload = false)
    {
        AnySchemaNode? node = RootNamespace;
        if (string.IsNullOrWhiteSpace(schemaName))
        {
            if (!preload || RootNamespace.Schemas.Length > 0) return node;
            reload = true;
        }
        
        // gets the node
        string fullPath = "";
        foreach (string path in Regex.Split(schemaName.Trim().ToLowerInvariant(), @"\W+")
                     .Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            if (node is not NamespaceNode parent) return null;
            fullPath = !string.IsNullOrWhiteSpace(fullPath) ? $"{fullPath}.{path}" : path;
            
            // Gets the sub node
            if (parent.SchemaNodes.TryGetValue(path, out node)) continue;
            
            // system schema first
            NodeSchema? schema = await LoadSchemaAsync(fullPath);
            node = schema;
            if (node is null) return null;
            
            if (parent.SchemaNodes.TryAdd(path, node))
            {
                Logger.LogInformation($"Schema '{fullPath}' Loading.");
                node.Release();
                node.Status = SchemaNodeStatus.Ready;
                await node.LoadAsync(this, schema!, preload);
                reload = false;
                Logger.LogInformation($"Schema '{fullPath}' Loaded.");
            }
            else
            {
                node = parent.SchemaNodes[path];
                reload = false;
            }
        }
        if (!reload) return node;
        
        // reload the node
        NodeSchema? newSchema = await LoadSchemaAsync(fullPath);
        if (newSchema != null)
        {
            node.Display = newSchema.Display;
            node.Release();
            node.Status = SchemaNodeStatus.Ready;
            await node.LoadAsync(this, newSchema, preload);
        }
        return node;
    }

    /// <summary>
    /// Remove a node from cache
    /// </summary>
    public bool RemoveSchemaNode(string schemaName)
    {
        AnySchemaNode? node = RootNamespace;
        if (string.IsNullOrWhiteSpace(schemaName)) return false;
        
        // gets the node
        string[] paths = Regex.Split(schemaName.Trim().ToLowerInvariant(), @"\W+").Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
        foreach (string path in paths.SkipLast(1))
        {
            // Gets the sub node
            if (node is not NamespaceNode parent || !parent.SchemaNodes.TryGetValue(path, out node)) return false;
        }

        if (node is NamespaceNode ns)
        {
            if (ns.SchemaNodes.TryGetValue(paths.Last(), out AnySchemaNode? child))
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
    public async Task<AppNode?> GetAppNodeAsync(string name, bool reload = false, bool preload = false)
    {
        // From root
        AppNode? node = RootAppNode;
        if (string.IsNullOrWhiteSpace(name))
        {
            if (!preload || node.Apps is {  Length: > 0 }) return node;
            reload = true;
        }
        name = name.ToLowerInvariant();

        // Gets the node
        string fullPath = string.Empty;
        foreach (string path in Regex.Split(name, @"\W+").Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            AppNode parent = node;
            fullPath = !string.IsNullOrWhiteSpace(fullPath) ? $"{fullPath}.{path}" : path;
            if (parent.SubAppList != null && parent.SubAppList.TryGetValue(path, out node))
                continue;

            if (reload && !preload)
                return null;

            // Gets the category
            AppSchema? schema = await LoadAppSchemaAsync(fullPath);
            if (schema == null) return null;
            node = new AppNode { Name = schema.Name };

            parent.SubAppList ??= new ConcurrentDictionary<string, AppNode>();

            if (parent.SubAppList.TryAdd(path, node))
            {
                Logger.LogDebug($"[Application]{node.Name} Loading.");
                await node.LoadAsync(this, schema, preload);
                reload = false;
                Logger.LogDebug($"[Application]{node.Name} Loaded.");
            }
            else
            {
                node = parent.SubAppList[path];
                reload = false;
            }
        }
        if (!reload) return node;

        // reload the node
        AppSchema? appSchema = await LoadAppSchemaAsync(fullPath);
        if (appSchema == null) return node;
        await node.LoadAsync(this, appSchema, preload);
        return node;
    }

    /// <summary>
    /// Remove an app from cache
    /// </summary>
    /// <param name="appName"></param>
    /// <returns></returns>
    public bool RemoveAppNode(string appName)
    {
        AppNode? node = RootAppNode;
        if (string.IsNullOrWhiteSpace(appName)) return false;

        // gets the node
        string[] paths = Regex.Split(appName.Trim().ToLowerInvariant(), @"\W+").Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
        foreach (string path in paths.SkipLast(1))
        {
            // Gets the sub node
            if (node.SubAppList == null || !node.SubAppList.TryGetValue(path, out node)) return false;
        }

        if (node.SubAppList is null) return false;

        if (node.SubAppList.TryGetValue(paths.Last(), out AppNode? child))
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
    public async Task<DynamicTableSchema> PrepareFieldDataAsync(AppFieldNode field)
    {
        // no front only & enable & no source ref
        if ((field.Frontend ?? false) || (field.Disable ?? false) || field.SourceNode != null)
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
    public async Task<List<DynamicTableSchema>> PrepareFieldDataAsync(AppNode node)
    {
        List<DynamicTableSchema> schemaList = new();
        if (node.Fields == null) return schemaList;
        
        // prepare the fields
        foreach (AppFieldNode field in node.Fields)
            schemaList.Add(await PrepareFieldDataAsync(field));

        // prepare the ref field
        if (node.RefField != null)
            await PrepareFieldDataAsync(node.RefField);
        return schemaList;
    }

    #endregion

    #region Data Management

    /// <summary>
    /// Sets the ref target of the field
    /// </summary>
    public async Task<bool> SetSourceFieldNode(AppFieldNode field, string target, string sourceTarget)
    {
        if (field.SourceNode == null) return false;
        AppNode? category = await GetAppNodeAsync(field.App);
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
    public async Task<bool> SetSourceFieldNode(AppNode app, string target, string sourceApp, string sourceTarget)
    {
        AppFieldNode? field = app.Fields?.FirstOrDefault(f => sourceApp.Equals(f.SourceApp, StringComparison.OrdinalIgnoreCase));
        return field == null || await SetSourceFieldNode(field, target, sourceTarget);
    }

    /// <summary>
    /// Sets the ref target of the field
    /// </summary>
    public async Task<bool> SetSourceFieldNode(string app, string target, string sourceApp, string sourceTarget)
    {
        AppNode? node = await GetAppNodeAsync(app);
        return node == null || await SetSourceFieldNode(node, target, sourceApp, sourceTarget);
    }

    /// <summary>
    /// Gets the source field node
    /// </summary>
    public async Task<(AppFieldNode?, string)> GetSourceFieldNode(AppFieldNode? field, string target, bool forPush = false)
    {
        if (field?.SourceNode == null) return (field, target);
        AppNode? category = await GetAppNodeAsync(field.App);

        // Means the category is front only and use the source node's target as target
        if (category?.RefField == null) return forPush ? (null, string.Empty) : await GetSourceFieldNode(field.SourceNode, target);

        JsonObject query = new() { { APP_FIELD_REF_APP, field.SourceNode.App } };
        (JsonNode? refData, _) = await GetFieldDataAsync(category.RefField, target, query);
        if (refData is JsonArray { Count: > 0 } arr && arr[0] is JsonObject jObject && jObject.TryGetValue(APP_FIELD_REF_TARGET, out JsonNode? val) && val is JsonValue && !val.IsEmpty())
        {
            string reftarget = val.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(reftarget))
            {
                return await GetSourceFieldNode(field.SourceNode, reftarget, forPush);
            }
        }

        // Consider use the same target if no ref for view
        return forPush ? (null, string.Empty) : await GetSourceFieldNode(field.SourceNode, target);
    }

    /// <summary>
    /// Save the field data by data
    /// </summary>
    public async Task<bool> SaveFieldDataAsync(AppFieldNode field, string target, JsonNode? value = null, bool innerCall = false)
    {
        // no front only & enable & no source ref
        if ((field.Frontend ?? false) || (field.Disable ?? false) || field.SourceNode != null) return false;

        // Not allow the direct data update
        if (!innerCall && !string.IsNullOrWhiteSpace(field.Func)) return false;
        if (AppDataProvider == null) throw new InvalidOperationException(APP_DATA_PROVIDER_NOT_EXIST);
        
        // Prepare
        DynamicTableSchema schema = await PrepareFieldDataAsync(field);

        try
        {
            (bool result, JsonNode? origin) = await AppDataProvider.SaveDynamicTableDataAsync(schema, target, value);
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
    /// Delete the list from a list-struct type field data
    /// </summary>
    public async Task DeleteFieldListDataAsync(AppFieldNode field, string target, JsonArray query, bool innerCall = false)
    {
        // no front only & enable & no source ref
        if ((field.Frontend ?? false) || (field.Disable ?? false) || field.SourceNode != null) return;
        if (AppDataProvider == null) throw new InvalidOperationException(APP_DATA_PROVIDER_NOT_EXIST);
        
        // Prepare
        DynamicTableSchema schema = await PrepareFieldDataAsync(field);

        // Only non-single schema can be used
        if (schema.Single) return;
        try
        {
            (bool result, JsonNode? origin) = await AppDataProvider.DeleteDynamicTableDataAsync(schema, target, query);
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
    public async Task DeleteFieldDataAsync(AppFieldNode field, string target, bool innerCall = false)
    {
        // no front only & enable & no source ref
        if ((field.Frontend ?? false) || (field.Disable ?? false) || field.SourceNode != null) return;
        if (AppDataProvider == null) throw new InvalidOperationException(APP_DATA_PROVIDER_NOT_EXIST);
        
        // Prepare
        DynamicTableSchema schema = await PrepareFieldDataAsync(field);
        
        try
        {
            (bool result, JsonNode? origin) = await AppDataProvider.DeleteDynamicTableDataAsync(schema, target);
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
    /// Gets the field data
    /// </summary>
    public async Task<(JsonNode? value, int total)> GetFieldDataAsync(AppFieldNode? field, string target, JsonNode? filter = null, int skip = 0, int take = 0, bool desc = false, AppSchemaDataOrder[]? orderBy = null)
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
            
            (JsonNode? result, int total) = await AppDataProvider.QueryDynamicTableAsync(schema, target, filter, skip, take, desc, orderBy);
            
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
    public async Task<Dictionary<string, Dictionary<string, List<FieldDataChangeData>>>> CommitTransactionAsync(bool pushAll = false, bool pushAllFields = false)
    {
        if (AppDataProvider == null) throw new InvalidOperationException(APP_DATA_PROVIDER_NOT_EXIST);
        
        // Gather change data
        Dictionary<string, Dictionary<string, List<FieldDataChangeData>>> commits = new();

        // Process data field push
        foreach (string target in _transChangedData.Keys.ToArray())
        {
            // Gather change datas
            Dictionary<string, List<FieldDataChangeData>> commitFields = new();
            foreach ((AppFieldNode field, List<FieldDataChangeData> data) in _transChangedData[target].Changes)
                commitFields[field.Name] = data;
            commits[target] = commitFields;

            // process data push
            await ProcessDataPush(target, _transChangedData[target], pushAll, pushAllFields);
        }

        await AppDataProvider.CommitTransactionAsync();

        // Return changes
        return commits;
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
    async Task ProcessDataPush(string target, TransactionChangeData changeData, bool pushAll = false, bool pushAllFields = false, AppFieldNode? pushNode = null)
    {
        // record the target
        Target = target;

        // Build the push generation
        List<AppFieldNode> baseFields = changeData.Changes.Keys.Where(p => p.Observers is { Count: > 0 }).ToList();

        // If push all
        if (pushAllFields)
        {
            baseFields.Clear();
            foreach (string app in changeData.Changes.Keys.Select(p => p.App).Distinct())
            {
                AppNode? appNode = await GetAppNodeAsync(app);
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
            foreach (AppFieldNode node in baseFields.Where(p => p.Observers != null).SelectMany(p => p.Observers!).Distinct().Where(n => !(n.Disable ?? false) && !(n.Frontend ?? false)))
            {
                if (!updateFieldsLvlMap.ContainsKey(node.Name))
                {
                    next.Fields.Add(node);
                    updateFieldsLvlMap.Add(node.Name, next);
                }
                else
                {
                    // Move the field to current
                    AppFieldNode item = updateFieldsLvlMap[node.Name].Fields.First(p => p.Name == node.Name);
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
        Dictionary<AppFieldNode, JsonNode> otherFields = new();
        HashSet<AppFieldNode> displayOnlyGens = new();
        HashSet<string> otherTargets = new();
        while (root?.Fields.Count is > 0)
        {
            foreach (AppFieldNode field in root.Fields)
            {
                // Check ref
                AppFieldNode? tarField = field;
                string realTarget = target;
                if (field.SourceNode != null)
                {
                    (tarField, realTarget) = await GetSourceFieldNode(field, target, true);
                    if (tarField == null) continue;
                    if (realTarget != target) otherTargets.Add(realTarget);
                }

                // Prepare arguments
                FunctionNode? funcNode = field.FuncNode;
                if (funcNode == null || field.FuncArgs == null) continue;
                FieldDataPushArg[] args = new FieldDataPushArg[field.FuncArgs.Count];
                int arrayIndex = -1;
                for (int i = 0; i < field.FuncArgs.Count; i++)
                {
                    AppFieldNodeArgument call = field.FuncArgs[i];
                    args[i] = new FieldDataPushArg();

                    // Generate argument
                    List<FieldDataChangeData>? changes = (!pushAll || field.SourceNode != null) && changeData.Changes.ContainsKey(call.AppField) ? changeData.Changes[call.AppField] : null;
                    args[i].Type = call.AppField.TypeNode!;
                    if (args[i].Type is ArrayNode && (funcNode.Args[i].TypeNode is not ArrayNode || arrayIndex < 0)) arrayIndex = i;

                    // Check changes
                    if (changes == null)
                    {
                        args[i].IsFull = true;
                        args[i].Changed = false;

                        // full data
                        if (otherFields.ContainsKey(call.AppField))
                        {
                            args[i].Value = otherFields[call.AppField].IsEmpty() ? null : otherFields[call.AppField];
                        }
                        else
                        {
                            (args[i].Value, _) = await GetFieldDataAsync(call.AppField, target);
                            otherFields[call.AppField] = args[i].Value ?? call.AppField.TypeNode?.Type switch
                            {
                                SchemaType.Scalar => JsonValue.Create((string?)null)!,
                                SchemaType.Enum => JsonValue.Create((string?)null)!,
                                SchemaType.Struct => new JsonObject(),
                                SchemaType.Array => new JsonArray(),
                                _ => JsonValue.Create((string?)null)!
                            };
                        }
                        args[i].Origin = args[i].Value;
                    }
                    else
                    {
                        // generate display only fields for upload datas
                        if (displayOnlyGens.Add(call.AppField))
                        {
                            // check schema
                            if (call.AppField.TypeNode is ArrayNode { ElementNode: StructNode } or StructNode)
                            {
                                DynamicTableSchema schema = await PrepareFieldDataAsync(call.AppField);
                                foreach (FieldDataChangeData change in changes)
                                {
                                    // for new
                                    if (change.Value is JsonArray varr)
                                    {
                                        foreach (JsonNode? token in varr)
                                        {
                                            if (token is JsonObject obj && !obj.IsEmpty())
                                            {
                                                await schema.GenerateDisplayOnlyFields(this, obj);
                                            }
                                        }
                                    }
                                    else if (change.Value is JsonObject vobj && !vobj.IsEmpty())
                                    {
                                        await schema.GenerateDisplayOnlyFields(this, vobj);
                                    }

                                    // for origin
                                    if (change.Origin is JsonArray oarr)
                                    {
                                        foreach (JsonNode? token in oarr)
                                        {
                                            if (token is JsonObject obj && !obj.IsEmpty())
                                            {
                                                await schema.GenerateDisplayOnlyFields(this, obj);
                                            }
                                        }
                                    }
                                    else if (change.Origin is JsonObject gobj && !gobj.IsEmpty())
                                    {
                                        await schema.GenerateDisplayOnlyFields(this, gobj);
                                    }
                                }
                            }
                        }

                        args[i].Changed = true;
                        if (call.AppField.TypeNode is ArrayNode)
                        {
                            // Check array if need part update
                            JsonArray values = new();
                            JsonArray origins = new();
                            foreach (FieldDataChangeData change in changes)
                            {
                                switch (change.Operation)
                                {
                                    case TransactionChangeOperation.Create:
                                        if (!change.Value.IsEmpty())
                                        {
                                            if (change.Value is JsonArray varr)
                                            {
                                                //  For array without primary keys
                                                args[i].IsFull = true;
                                                values = varr;
                                            }
                                            else
                                            {
                                                values.Add(change.Value!.DeepClone());
                                            }
                                        }
                                        break;
                                    case TransactionChangeOperation.Modify:
                                        if (!change.Value.IsEmpty())
                                        {
                                            if (change.Value is JsonArray varr)
                                            {
                                                //  For array without primary keys
                                                args[i].IsFull = true;
                                                values = varr;
                                            }
                                            else
                                            {
                                                values.Add(change.Value!.DeepClone());
                                            }
                                        }
                                        if (!change.Origin.IsEmpty())
                                        {
                                            if (change.Origin is JsonArray varr)
                                            {
                                                //  For array without primary keys
                                                args[i].IsFull = true;
                                                origins = varr;
                                            }
                                            else
                                            {
                                                origins.Add(change.Origin!.DeepClone());
                                            }
                                        }
                                        break;
                                    case TransactionChangeOperation.Delete:
                                        if (!change.Origin.IsEmpty())
                                        {
                                            if (change.Origin is JsonArray varr)
                                            {
                                                //  For array without primary keys
                                                args[i].IsFull = true;
                                                origins = varr;
                                            }
                                            else
                                            {
                                                origins.Add(change.Origin!.DeepClone());
                                            }
                                        }
                                        break;
                                    case TransactionChangeOperation.DropAll:
                                        args[i].IsFull = true;
                                        if (change.Origin is JsonArray arr)
                                            origins = arr;
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
                        if (args[i].Type is StructNode)
                        {
                            // Gets the value
                            args[i].Value = args[i].Value.GetValueByPaths(call.DataField);

                            // Gets the origin
                            args[i].Origin = args[i].Origin.GetValueByPaths(call.DataField);
                        }
                        else if (args[i].Type is ArrayNode { ElementNode: StructNode })
                        {
                            // Gets the value
                            if (args[i].Value is JsonArray arr)
                            {
                                for (int h = 0; h < arr.Count; h++)
                                {
                                    arr[h] = arr[h].GetValueByPaths(call.DataField);
                                }
                            }

                            // Gets the origin
                            if (args[i].Origin is JsonArray oarr)
                            {
                                for (int h = 0; h < oarr.Count; h++)
                                {
                                    oarr[h] = oarr[h].GetValueByPaths(call.DataField);
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
                        arg.Value = otherFields[call.AppField].IsEmpty() ? null : otherFields[call.AppField];
                    }
                    else
                    {
                        (arg.Value, _) = await GetFieldDataAsync(call.AppField, target);
                        otherFields[call.AppField] = arg.Value ?? call.AppField.TypeNode?.Type switch
                        {
                            SchemaType.Scalar => JsonValue.Create((string?)null)!,
                            SchemaType.Enum => JsonValue.Create((string?)null)!,
                            SchemaType.Struct => new JsonObject(),
                            SchemaType.Array => new JsonArray(),
                            _ => JsonValue.Create((string?)null)!
                        };
                    }
                    arg.Origin = arg.Value;
                    arg.IsFull = true;
                }

                // If part update or is ref, must get the original calc result
                JsonNode? oldResult = null;
                if (arrayIndex >= 0) // && (!args[arrayIndex].IsFull || field.SourceNode != null))
                {
                    JsonArray originCall = new();
                    foreach (FieldDataPushArg arg in args)
                        originCall.Add(arg.Origin?.DeepClone());

                    // Check if use element
                    if (funcNode.Args[arrayIndex].TypeNode is not ArrayNode)
                    {
                        JsonArray resultArr = new();
                        if (args[arrayIndex].Origin is JsonArray origin)
                        {
                            foreach (JsonNode? t in origin)
                            {
                                if (t == null) continue;
                                originCall[arrayIndex] = t.DeepClone();
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

                        oldResult = resultArr;
                    }
                    else
                    {
                        oldResult = await CallFunctionAsync(field.FuncNode!, originCall);
                    }
                }

                // Calc the new result
                JsonNode? newResult;
                JsonArray callArgs = new();
                foreach (FieldDataPushArg arg in args)
                    callArgs.Add(arg.Value?.DeepClone());

                // Check if use element
                if (arrayIndex >= 0 && funcNode.Args[arrayIndex].TypeNode is not ArrayNode)
                {
                    JsonArray resultArr = new();
                    if (args[arrayIndex].Value is JsonArray origin)
                    {
                        foreach (JsonNode? t in origin)
                        {
                            if (t == null) continue;
                            callArgs[arrayIndex] = t.DeepClone();
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

                    newResult = resultArr;
                }
                else
                {
                    newResult = await CallFunctionAsync(field.FuncNode!, callArgs);
                }

                // Join the result
                JsonNode? result = null;
                switch (field.TypeNode)
                {
                    case EnumNode @enum:
                        {
                            // Can't join the result, only directly assignment allowed
                            if (newResult is JsonValue)
                            {
                                (_, JsonNode? error) = await @enum.ValidateValueAsync(this, newResult);
                                result = error.IsEmpty() ? newResult : throw new Exception(error!.ToString());
                            }
                            break;
                        }
                    case ScalarNode scalar:
                        {
                            // Gets the join method
                            DataCombineType method = field.Combine ?? (scalar.IsNumber ? DataCombineType.Sum : DataCombineType.Assign);
                            if (arrayIndex < 0 || args[arrayIndex].IsFull)
                            {
                                // Full
                                result = await GroupJoin(scalar, newResult, method);
                            }
                            else
                            {
                                // Part
                                (JsonNode? origin, _) = await GetFieldDataAsync(tarField, realTarget);
                                JsonValue? old = await GroupJoin(scalar, oldResult, method);
                                JsonValue? now = await GroupJoin(scalar, newResult, method);

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
                                            result = origin.IsEmpty() ? now : origin;
                                            break;
                                        }
                                    case DataCombineType.Sum:
                                        {
                                            if (scalar.IsInt)
                                            {
                                                result = (!origin.IsEmpty() ? ((JsonValue)origin!).GetValue<int>() : 0)
                                                         + (!now.IsEmpty() ? (now!).GetValue<int>() : 0)
                                                         - (!old.IsEmpty() ? (old!).GetValue<int>() : 0);
                                            }
                                            else if (scalar.IsSingle)
                                            {
                                                result = (!origin.IsEmpty() ? ((JsonValue)origin!).GetValue<float>() : 0)
                                                         + (!now.IsEmpty() ? (now!).GetValue<float>() : 0)
                                                         - (!old.IsEmpty() ? (old!).GetValue<float>() : 0);
                                            }
                                            else if (scalar.IsDouble)
                                            {
                                                result = (!origin.IsEmpty() ? ((JsonValue)origin!).GetValue<double>() : 0)
                                                         + (!now.IsEmpty() ? (now!).GetValue<double>() : 0)
                                                         - (!old.IsEmpty() ? (old!).GetValue<double>() : 0);
                                            }
                                            else
                                            {
                                                result = (!origin.IsEmpty() ? ((JsonValue)origin!).GetValue<decimal>() : 0)
                                                         + (!now.IsEmpty() ? (now!).GetValue<decimal>() : 0)
                                                         - (!old.IsEmpty() ? (old!).GetValue<decimal>() : 0);
                                            }
                                        }
                                        break;
                                    case DataCombineType.Count:
                                        {
                                            result = (!origin.IsEmpty() ? ((JsonValue)origin!).GetValue<int>() : 0)
                                                     + (!now.IsEmpty() ? (now!).GetValue<int>() : 0)
                                                     - (!old.IsEmpty() ? (old!).GetValue<int>() : 0);
                                            break;
                                        }
                                    default:
                                        throw new ArgumentOutOfRangeException();
                                }
                            }
                            break;
                        }
                    case StructNode { Fields.Length: > 0 } @struct:
                        {
                            // Gets the join method map
                            Dictionary<string, DataCombineType> joinMethodMap = new();

                            // Default join
                            foreach (StructFieldConfig f in @struct.Fields)
                            {
                                if (f.TypeNode is ScalarNode s)
                                    joinMethodMap[f.Name] = field.Combines?.FirstOrDefault(o => o.Field.Equals(f.Name, StringComparison.OrdinalIgnoreCase))?.Type 
                                        ?? (s.IsNumber ? DataCombineType.Sum : DataCombineType.Assign);
                            }

                            // Gets the result
                            if (arrayIndex < 0 || args[arrayIndex].IsFull)
                            {
                                // Full
                                result = await GroupJoin(@struct, newResult, joinMethodMap);
                            }
                            else
                            {
                                // Part
                                (JsonNode? origin, _) = await GetFieldDataAsync(tarField, realTarget);
                                JsonObject? old = await GroupJoin(@struct, oldResult, joinMethodMap);
                                JsonObject? now = await GroupJoin(@struct, newResult, joinMethodMap);

                                // Update with join method
                                if (origin.IsEmpty() && old.IsEmpty())
                                {
                                    result = now;
                                }
                                else
                                {
                                    JsonObject final = !origin.IsEmpty() ? (JsonObject)origin!.DeepClone() : new JsonObject();
                                    foreach (StructFieldConfig nodeField in @struct.Fields)
                                    {
                                        switch (joinMethodMap.GetValueOrDefault(nodeField.Name, DataCombineType.Assign))
                                        {
                                            case DataCombineType.Assign:
                                                {
                                                    if (!now.IsEmpty() && now!.ContainsKey(nodeField.Name))
                                                        final[nodeField.Name] = now[nodeField.Name]?.DeepClone();
                                                    //else if (final.ContainsKey(nodeField.Name))
                                                    //    final.Remove(nodeField.Name);
                                                    break;
                                                }
                                            case DataCombineType.Init:
                                                {
                                                    if (origin.IsEmpty() && !now.IsEmpty() && now!.ContainsKey(nodeField.Name))
                                                        final[nodeField.Name] = now[nodeField.Name]?.DeepClone();
                                                    break;
                                                }
                                            case DataCombineType.Sum when nodeField.TypeNode is ScalarNode { IsNumber: true }:
                                                {
                                                    decimal sum = 0m;
                                                    if (origin is JsonObject originObj && originObj.ContainsKey(nodeField.Name) && originObj[nodeField.Name] is JsonValue oval && !oval.IsEmpty())
                                                        sum = oval.GetValue<decimal>();
                                                    if (!old.IsEmpty() && old!.ContainsKey(nodeField.Name) && old[nodeField.Name] is JsonValue olval && !olval.IsEmpty())
                                                        sum -= olval.GetValue<decimal>();
                                                    if (!now.IsEmpty() && now!.ContainsKey(nodeField.Name) && now[nodeField.Name] is JsonValue nval && !nval.IsEmpty())
                                                        sum += nval.GetValue<decimal>();
                                                    final[nodeField.Name] = sum;
                                                    break;
                                                }
                                            case DataCombineType.Count when nodeField.TypeNode is ScalarNode { IsNumber: true }:
                                                {
                                                    int sum = 0;
                                                    if (origin is JsonObject originObj && originObj.ContainsKey(nodeField.Name) && originObj[nodeField.Name] is JsonValue oval && !oval.IsEmpty())
                                                        sum = oval.GetValue<int>();
                                                    if (!old.IsEmpty() && old!.ContainsKey(nodeField.Name) && old[nodeField.Name] is JsonValue olval && !olval.IsEmpty())
                                                        sum -= olval.GetValue<int>();
                                                    if (!now.IsEmpty() && now!.ContainsKey(nodeField.Name) && now[nodeField.Name] is JsonValue nval && !nval.IsEmpty())
                                                        sum += nval.GetValue<int>();
                                                    final[nodeField.Name] = sum;
                                                    break;
                                                }
                                            default:
                                                throw new ArgumentOutOfRangeException();
                                        }
                                    }
                                    result = final;
                                }
                            }
                            break;
                        }
                    case ArrayNode { ElementNode: EnumNode or ScalarNode } array:
                        {
                            // simple array, use the new result directly, can't join the data, normally this case won't be really used.
                            // There is no primary key to track which should be removed, which should be updated
                            if (newResult is JsonArray { Count: > 0 } arr)
                            {
                                (object? res, JsonNode? error) = await array.ValidateValueAsync(this, arr);
                                result = error.IsEmpty() ? res as JsonArray : throw new Exception(error!.ToString());
                            }
                            break;
                        }
                    case ArrayNode { ElementNode: StructNode { Fields: { Length: > 0 } } structNode, Primary: { Length: > 0 } } array:
                        {
                            // Gets the join method map
                            Dictionary<string, DataCombineType> joinMethodMap = new();
                            Dictionary<string, JsonObject> resultMap;
                            bool isFull = arrayIndex < 0 || args[arrayIndex].IsFull;

                            // Gets the value fields
                            List<string> valueFields = new();
                            Dictionary<string, AnySchemaNode> primaryNodes = new();
                            foreach (StructFieldConfig fieldType in structNode.Fields)
                            {
                                if (!array.Primary.Contains(fieldType.Name))
                                {
                                    valueFields.Add(fieldType.Name);

                                    if (fieldType.TypeNode is ScalarNode s)
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
                            if (isFull && false)
                            {
                                // Full
                                resultMap = await GroupJoinObjectMap(array, newResult, joinMethodMap);
                            }
                            else
                            {
                                // Part

                                // Group join the old & now data
                                Dictionary<string, JsonObject> oldMap = await GroupJoinObjectMap(array, oldResult, joinMethodMap);
                                Dictionary<string, JsonObject> nowMap = await GroupJoinObjectMap(array, newResult, joinMethodMap);

                                // Query the original data
                                HashSet<string> keys = new();
                                JsonArray query = new();
                                foreach ((string key, JsonObject obj) in oldMap)
                                {
                                    if (!keys.Add(key)) continue;
                                    query.Add(obj.DeepClone());
                                }
                                foreach ((string key, JsonObject obj) in nowMap)
                                {
                                    if (!keys.Add(key)) continue;
                                    query.Add(obj.DeepClone());
                                }

                                // Gets the original data
                                resultMap = new Dictionary<string, JsonObject>();
                                if (!query.IsEmpty())
                                {
                                    (JsonNode? value, _) = await GetFieldDataAsync(tarField, realTarget, query);
                                    if (value is JsonArray arr)
                                    {
                                        foreach (JsonNode? token in arr)
                                        {
                                            if (token is not JsonObject obj) continue;
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
                                        JsonObject? old = oldMap.ContainsKey(key) ? oldMap[key] : null;
                                        JsonObject? now = nowMap.ContainsKey(key) ? nowMap[key] : null;
                                        foreach (string s in valueFields)
                                        {
                                            switch (joinMethodMap.GetValueOrDefault(s, DataCombineType.Assign))
                                            {
                                                case DataCombineType.Assign:
                                                    if (!now.IsEmpty() && now!.ContainsKey(s))
                                                        res1[s] = now[s]?.DeepClone();
                                                    //else if (res.ContainsKey(s))
                                                    //    res.Remove(s);
                                                    break;
                                                case DataCombineType.Init:
                                                    break;
                                                case DataCombineType.Sum:
                                                    res1[s] = (res1.ContainsKey(s) && !res1[s].IsEmpty() ? res1[s]!.GetValue<decimal>() : 0) +
                                                              (!now.IsEmpty() && now!.ContainsKey(s) && !now[s].IsEmpty() ? now[s]!.GetValue<decimal>() : 0) -
                                                              (!old.IsEmpty() && old!.ContainsKey(s) && !old[s].IsEmpty() ? old[s]!.GetValue<decimal>() : 0);
                                                    break;
                                                case DataCombineType.Count:
                                                    res1[s] = (res1.ContainsKey(s) && !res1[s].IsEmpty() ? res1[s]!.GetValue<long>() : 0) +
                                                              (!now.IsEmpty() && now!.ContainsKey(s) && !now[s].IsEmpty() ? now[s]!.GetValue<long>() : 0) -
                                                              (!old.IsEmpty() && old!.ContainsKey(s) && !old[s].IsEmpty() ? old[s]!.GetValue<long>() : 0);
                                                    break;
                                                default:
                                                    throw new ArgumentOutOfRangeException();
                                            }
                                        }
                                    }
                                    else if (nowMap.ContainsKey(key))
                                    {
                                        resultMap.Add(key, nowMap[key]);
                                        if (!oldMap.ContainsKey(key))
                                            continue;

                                        // Shouldn't be but still handle it
                                        JsonObject old = oldMap[key];
                                        JsonObject res = resultMap[key];

                                        foreach (string s in valueFields)
                                        {
                                            switch (joinMethodMap.GetValueOrDefault(s, DataCombineType.Assign))
                                            {
                                                case DataCombineType.Assign:
                                                    break;
                                                case DataCombineType.Init:
                                                    if (!old.IsEmpty() && old.ContainsKey(s))
                                                        res[s] = old[s]?.DeepClone();
                                                    break;
                                                case DataCombineType.Sum:
                                                    res[s] = (res.ContainsKey(s) && !res[s].IsEmpty() ? res[s]!.GetValue<decimal>() : 0) -
                                                             (!old.IsEmpty() && old.ContainsKey(s) && !old[s].IsEmpty() ? old[s]!.GetValue<decimal>() : 0);
                                                    break;
                                                case DataCombineType.Count:
                                                    res[s] = (res.ContainsKey(s) && !res[s].IsEmpty() ? res[s]!.GetValue<long>() : 0) -
                                                             (!old.IsEmpty() && old.ContainsKey(s) && !old[s].IsEmpty() ? old[s]!.GetValue<long>() : 0);
                                                    break;
                                                default:
                                                    throw new ArgumentOutOfRangeException();
                                            }
                                        }
                                    }
                                }
                            }

                            // Convert the map to list, sorted by primary keys
                            JsonArray joinArray = new();
                            List<JsonObject> joinObjs = resultMap.Values.ToList();
                            joinObjs.Sort((a, b) =>
                            {
                                foreach (string s in array.Primary)
                                {
                                    switch (primaryNodes[s])
                                    {
                                        case ScalarNode { IsDate: true }:
                                            {
                                                DateTime ad = a[s]!.GetValue<DateTime>();
                                                DateTime bd = b[s]!.GetValue<DateTime>();
                                                if (!ad.Equal(bd))
                                                    return ad.LessThan(bd) ? -1 : 1;
                                                break;
                                            }
                                        case ScalarNode { IsNumber: true }:
                                            {
                                                decimal ad = a[s]!.GetValue<decimal>();
                                                decimal bd = b[s]!.GetValue<decimal>();
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
                            foreach (JsonObject o in joinObjs)
                                joinArray.Add(o.DeepClone());

                            // Save to result
                            result = joinArray;
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
    void OnFieldDataChanged(string target, AppFieldNode field, TransactionChangeOperation operation, JsonNode? value = null, JsonNode? origin = null)
    {
        TransactionChangeData changeData;
        if (_transChangedData.ContainsKey(target))
        {
            changeData = _transChangedData[target];
        }
        else
        {
            changeData = new TransactionChangeData();
            _transChangedData.Add(target, changeData);
        }
        if (changeData.Changes.ContainsKey(field))
        {
            changeData.Changes[field].Add(new FieldDataChangeData(operation, value, origin));
        }
        else
        {
            changeData.Changes.Add(field, new List<FieldDataChangeData>
            {
                new(operation, value, origin)
            });
        }
    }

    #endregion

    #region Group Join

    /// <summary>
    /// Join to scalar
    /// </summary>
    public async Task<JsonValue?> GroupJoin(ScalarNode node, JsonNode? value, DataCombineType method)
    {
        JsonValue? result = value switch
        {
            // Direct
            JsonValue val => val,
            // Join
            JsonArray { Count: > 0 } newArray =>
                // Get by join methods
                method switch
                {
                    // Join the data
                    DataCombineType.Assign => newArray.LastOrDefault(p => p is JsonValue) as JsonValue,
                    DataCombineType.Count => JsonValue.Create(newArray.Count),
                    DataCombineType.Sum => node.IsNumber ? JsonValue.Create(newArray.Sum(token => token?.GetValue<decimal>() ?? 0)) : null,
                    DataCombineType.Init => newArray.FirstOrDefault(p => p is JsonValue) as JsonValue,
                    _ => throw new ArgumentOutOfRangeException(nameof(method), method, null)
                },
            _ => null
        };
        if (result.IsEmpty()) return null;
        (object? res, JsonNode? error) = await node.ValidateValueAsync(this, result!);
        return error.IsEmpty() ? JsonValue.Create(res) : throw new Exception(error!.ToString());
    }

    /// <summary>
    /// Join to struct
    /// </summary>
    public async Task<JsonObject?> GroupJoin(StructNode node, JsonNode? value, IReadOnlyDictionary<string, DataCombineType> joinMethodMap)
    {
        if (value.IsEmpty() || node.Fields.Length == 0) return null;
        switch (value)
        {
            case JsonObject:
                {
                    (object? res, JsonNode? error) = await node.ValidateValueAsync(this, value);
                    if (!error.IsEmpty()) throw new Exception(error!.ToString());
                    JsonObject result = (JsonObject)res.ToJsonNode()!;
                    // count field
                    foreach ((string field, DataCombineType method) in joinMethodMap)
                    {
                        if (method == DataCombineType.Count && node.Fields.FirstOrDefault(f => f.Name.Equals(field, StringComparison.OrdinalIgnoreCase)) is StructFieldConfig { TypeNode: ScalarNode { IsNumber: true } })
                        {
                            result[field] = 1;
                        }
                    }
                    return result;
                }
            case JsonArray { Count: > 0 } array:
                {
                    // Valiate the result
                    JsonArray validateArray = new();
                    foreach (JsonNode? token in array)
                    {
                        if (token == null) continue;
                        (object? res, JsonNode? error) = await node.ValidateValueAsync(this, token);
                        validateArray.Add(error.IsEmpty() ? res : throw new Exception(error!.ToString()));
                    }
                    array = validateArray;
                    if (array.IsEmpty()) return null;

                    // Join
                    JsonObject result = new();
                    foreach (StructFieldConfig field in node.Fields)
                    {
                        switch (joinMethodMap.GetValueOrDefault(field.Name, DataCombineType.Assign))
                        {
                            case DataCombineType.Assign:
                                {
                                    JsonObject? last = (JsonObject?)array.LastOrDefault(p => p is JsonObject obj && obj.ContainsKey(field.Name));
                                    if (last != null)
                                        result[field.Name] = last[field.Name];
                                    break;
                                }
                            case DataCombineType.Init:
                                {
                                    JsonObject? first = (JsonObject?)array.FirstOrDefault(p => p is JsonObject obj && obj.ContainsKey(field.Name));
                                    if (first != null)
                                        result[field.Name] = first[field.Name];
                                    break;
                                }
                            case DataCombineType.Sum:
                                result[field.Name] = field.TypeNode is ScalarNode { IsNumber: true } ? array.Sum(p => p is JsonObject obj && obj.ContainsKey(field.Name) && obj[field.Name] is JsonValue val && !val.IsEmpty() ? val.GetValue<decimal>() : 0) : null;
                                break;
                            case DataCombineType.Count:
                                result[field.Name] = field.TypeNode is ScalarNode { IsNumber: true } ? array.Count : null;
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }
                    return result;
                }
        }
        return null;
    }

    /// <summary>
    /// Join to array
    /// </summary>
    public async Task<Dictionary<string, JsonObject>> GroupJoinObjectMap(ArrayNode node, JsonNode? value, Dictionary<string, DataCombineType> joinMethodMap)
    {
        if (value.IsEmpty()) return new Dictionary<string, JsonObject>();

        // Gets field type
        StructNode @struct = (StructNode)node.ElementNode!;
        List<string> valueFields = (from fieldType in @struct.Fields where !node.Primary.Contains(fieldType.Name) select fieldType.Name).ToList();

        // The element struct type
        switch (value)
        {
            // Check by value
            case JsonObject o when !o.IsEmpty():
                {
                    // Validate the value
                    (object? res, JsonNode? error) = await @struct.ValidateValueAsync(this, o);
                    if (!error.IsEmpty()) throw new Exception(error!.ToString());
                    o = (JsonObject)res.ToJsonNode()!;

                    // Check the primary key
                    string? key = node.GetPrimaryKey(o);
                    if (string.IsNullOrWhiteSpace(key))
                        return new Dictionary<string, JsonObject>();

                    // Return single element array
                    return new Dictionary<string, JsonObject>
                    {
                        { key, o }
                    };
                }
            case JsonArray array:
                {
                    // The return list with order
                    Dictionary<string, JsonObject> keyMap = new();
                    Dictionary<string, int> keyCount = new();
                    foreach (JsonNode? token in array)
                    {
                        if (token is not JsonObject obj) continue;

                        // Validate the value
                        (object ?res, JsonNode? error) = await @struct.ValidateValueAsync(this, obj);
                        if (!error.IsEmpty()) throw new Exception(error!.ToString());
                        obj = (JsonObject)res.ToJsonNode()!;

                        // Gets the key
                        string? key = node.GetPrimaryKey(obj);
                        if (string.IsNullOrWhiteSpace(key)) continue;
                        if (keyMap.TryGetValue(key, out JsonObject? total))
                        {
                            // Join the data fields
                            keyCount[key]++;
                            foreach (string s in valueFields)
                            {
                                switch (joinMethodMap.GetValueOrDefault(s, DataCombineType.Assign))
                                {
                                    case DataCombineType.Assign:
                                        {
                                            if (obj.ContainsKey(s) && !obj[s].IsEmpty())
                                                total[s] = obj[s];
                                            break;
                                        }

                                    case DataCombineType.Init:
                                        if (!(total.ContainsKey(s) && !total[s].IsEmpty()) && obj.ContainsKey(s) && !obj[s].IsEmpty())
                                            total[s] = obj[s];
                                        break;

                                    case DataCombineType.Sum:
                                        total[s] = (total.ContainsKey(s) && !total[s].IsEmpty() ? total[s]!.GetValue<decimal>() : 0) + (obj.ContainsKey(s) && !obj[s].IsEmpty() ? obj[s]!.GetValue<decimal>() : 0);
                                        break;

                                    case DataCombineType.Count:
                                        total[s] = (total.ContainsKey(s) && !total[s].IsEmpty() ? total[s]!.GetValue<int>() : 0) + 1;
                                        break;
                                    default:
                                        throw new ArgumentOutOfRangeException();
                                }
                            }
                        }
                        else
                        {
                            // Add to order list
                            keyMap[key] = obj;
                            keyCount[key] = 1;

                            // Init Count
                            foreach ((string s, DataCombineType m) in joinMethodMap)
                                if (m == DataCombineType.Count)
                                    obj[s] = 1;
                        }
                    }

                    // Gen the result
                    return keyMap;
                }
        }
        return new Dictionary<string, JsonObject>();
    }

    /// <summary>
    /// Join to array
    /// </summary>
    public async Task<JsonArray?> GroupJoin(ArrayNode node, JsonNode value, Dictionary<string, DataCombineType> joinMethodMap)
    {
        if (node.ElementNode is not StructNode structNode || node.Primary == null) return null;
        Dictionary<string, AnySchemaNode?> primaryNodes = structNode.Fields.Where(fieldType => node.Primary.Contains(fieldType.Name)).ToDictionary(fieldType => fieldType.Name, fieldType => fieldType.TypeNode);

        // Result
        JsonArray joinArray = new();
        Dictionary<string, JsonObject> resultMap = await GroupJoinObjectMap(node, value, joinMethodMap);
        List<JsonObject> joinObjs = resultMap.Values.ToList();
        joinObjs.Sort((a, b) =>
        {
            foreach (string s in node.Primary)
            {
                switch (primaryNodes[s])
                {
                    case ScalarNode { IsDate: true }:
                        {
                            DateTime ad = a[s]!.GetValue<DateTime>();
                            DateTime bd = b[s]!.GetValue<DateTime>();
                            if (!ad.Equal(bd))
                                return ad.LessThan(bd) ? -1 : 1;
                            break;
                        }
                    case ScalarNode { IsNumber: true }:
                        {
                            decimal ad = a[s]!.GetValue<decimal>();
                            decimal bd = b[s]!.GetValue<decimal>();
                            if (ad != bd)
                                return ad < bd ? -1 : 1;
                            break;
                        }
                    default:
                        {
                            string ad = a[s]?.ToString() ?? string.Empty;
                            string bd = b[s]?.ToString() ?? string.Empty;
                            if (!ad.Equals(bd))
                                return string.Compare(ad, bd, StringComparison.OrdinalIgnoreCase);
                            break;
                        }
                }
            }
            return 0;
        });
        foreach (JsonObject o in joinObjs)
            joinArray.Add(o);
        return joinArray;
    }

    #endregion

    #endregion

    #region Utility

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
    static readonly NamespaceNode RootNamespace;
    static readonly AppNode RootAppNode;

    #endregion
}