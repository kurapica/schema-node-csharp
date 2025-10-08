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
using System.Runtime.CompilerServices;
using System.Text;

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
                parentNode.Apps = parentNode.Apps == null ? [app] : parentNode.Apps.Concat([app]).ToArray();
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
        Logger.LogInformation("GetSchemaNodeAsync {0}", schemaName);
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
        foreach (string path in Regex.Split(name, @"\W+"))
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
        AppSchema? AppSchema = await LoadAppSchemaAsync(fullPath);
        if (AppSchema == null) return node;
        await node.LoadAsync(this, AppSchema, preload);
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

        if (node?.SubAppList is null) return false;

        if (node.SubAppList.TryGetValue(paths.Last(), out AppNode? child))
        {
            if (child.IsUsed) return false;
            node.SubAppList.TryRemove(paths.Last(), out child);
        }
        node.Apps = node.Apps?.Where(s => !s.Name.Equals(appName, StringComparison.OrdinalIgnoreCase)).ToArray() ?? [];
        return true;
    }

    #endregion

    #region Dynamic Data

    #region Table Management

    /// <summary>
    /// Preprare the dynamic table for the field
    /// </summary>
    async Task<DynamicTableSchema> PrepareFieldDataAsync(AppFieldNode field)
    {
        // no front only & enable & no source ref
        if ((field.Frontend ?? false) || (field.Disable ?? false) || field.SourceNode != null)
            return field.Schema ??= field.GenDynamicTableSchema();

        // creating and building
        if (MySql.Context.Database.GetDbConnection().State == ConnectionState.Closed)
            await MySql.Context.Database.GetDbConnection().OpenAsync();

        // Return the data
        DynamicTableSchema schema = field.Schema;
        if (schema != null) return schema;
        field.Schema = field.GenDynamicTableSchema();
        schema = field.Schema;

        // Check to update the data table
        try
        {
            // Gets the existed fields
            DbCommand command = GetDbCommand();
            command.CommandText = $"DESCRIBE `{schema.Name}`";
            DbDataReader reader = await command.ExecuteReaderAsync();
            Dictionary<string, string> nameTypes = new();
            try
            {
                while (await reader.ReadAsync())
                    nameTypes.Add(reader.GetString(0), reader.GetString(1));
            }
            finally
            {
                await reader.CloseAsync();
            }

            // Check the new schema
            StringBuilder? sb = null;
            foreach (DynamicTableField dyFld in schema.Fields)
            {
                if (!nameTypes.ContainsKey(dyFld.Name))
                {
                    sb ??= new StringBuilder();
                    sb.Append($"ALTER TABLE `{schema.Name}` ADD `{dyFld.Name}` {dyFld.DataType};");
                }
                else if (!nameTypes[dyFld.Name].Equals(dyFld.DataType, StringComparison.OrdinalIgnoreCase))
                {
                    sb ??= new StringBuilder();
                    sb.Append($"ALTER TABLE `{schema.Name}` MODIFY COLUMN `{dyFld.Name}` {dyFld.DataType};");
                }
            }

            // Check the existed indexes
            command = GetDbCommand();
            command.CommandText = $"SHOW INDEXES FROM `{schema.Name}`";
            reader = await command.ExecuteReaderAsync();
            Dictionary<string, bool> names = new(); // name => unique

            // Check indexes
            List<string> uniqueIndex = new();
            try
            {
                while (await reader.ReadAsync())
                {
                    string keyName = reader.GetString("Key_name");
                    if (keyName.Equals(DYNAMIC_UNIQUE_INDEX, StringComparison.OrdinalIgnoreCase))
                    {
                        uniqueIndex.Add(reader.GetString("Column_name"));
                    }
                    else if (!keyName.Equals("PRIMARY", StringComparison.OrdinalIgnoreCase) && !names.ContainsKey(keyName))
                    {
                        names.Add(keyName, reader.GetInt32("Non_unique") == 0);
                    }
                }
            }
            finally
            {
                await reader.CloseAsync();
            }

            // Check unique indexes
            if (!schema.Single)
            {
                List<string> chkUniqueIndex = new()
                {
                    DYNAMIC_TABLE_TARG_FIELD
                };
                foreach (DynamicTableField tableField in schema.Fields.Where(p => p.Primary))
                    chkUniqueIndex.Add(tableField.Name);

                // Compares the unique indexes
                if (chkUniqueIndex.Count != uniqueIndex.Count || chkUniqueIndex.Where((p, i) => !p.Equals(uniqueIndex[i])).Any())
                {
                    // Remove the old unique index
                    if (uniqueIndex.Count > 0)
                    {
                        sb ??= new StringBuilder();
                        sb.Append($"DROP INDEX `{DYNAMIC_UNIQUE_INDEX}` ON `{schema.Name}`;");
                    }

                    // Add the unique index
                    sb ??= new StringBuilder();
                    sb.Append($"ALTER TABLE `{schema.Name}` ADD UNIQUE INDEX `{DYNAMIC_UNIQUE_INDEX}`({string.Join(',', chkUniqueIndex.Select(e => $"`{e}`"))});");
                }
            }

            // Check new indexes
            if (schema.Indexes is { Count: > 0 })
            {
                foreach (DataTableIndex index in schema.Indexes)
                {
                    string key = $"IDX_{schema.Name}_{string.Join('_', index.Fields)}";
                    bool isUnique = index.Unique ?? false;
                    if (names.ContainsKey(key))
                    {
                        // Check unique
                        if (names[key] != isUnique)
                        {
                            sb ??= new StringBuilder();
                            sb.Append($"DROP INDEX `{key}` ON `{schema.Name}`;");
                            sb.Append($"ALTER TABLE `{schema.Name}` ADD {(isUnique ? "UNIQUE" : "INDEX")} `{key}`({string.Join(',', index.Fields.Select(e => $"`{e}`"))});");
                        }
                        names.Remove(key);
                    }
                    else
                    {
                        sb ??= new StringBuilder();
                        sb.Append($"ALTER TABLE `{schema.Name}` ADD {(isUnique ? "UNIQUE" : "INDEX")} `{key}`({string.Join(',', index.Fields.Select(e => $"`{e}`"))});");
                    }
                }
            }

            // Remove no use indexes
            foreach (string name in names.Keys.Where(p => !p.Equals(DYNAMIC_UNIQUE_INDEX)))
            {
                sb ??= new StringBuilder();
                sb.Append($"DROP INDEX `{name}` ON `{schema.Name}`;");
            }

            // Update the table
            if (sb != null)
            {
                DbCommand updateCommand = GetDbCommand();
                updateCommand.CommandText = sb.ToString();
                await updateCommand.ExecuteNonQueryAsync();
            }
            return schema;
        }
        catch (MySqlException ex)
        {
            // Continue to create the table
            if (ex.ErrorCode != MySqlErrorCode.NoSuchTable)
                throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.Message);
            throw;
        }

        // Create the data table
        try
        {
            StringBuilder sb = new();

            // Creaate the data table
            sb.Append($"CREATE TABLE IF NOT EXISTS `{schema.Name}` (");

            // The primary key
            if (!schema.Single)
                sb.Append($"`{DYNAMIC_TABLE_SEQNO_FIELD}` INT UNSIGNED AUTO_INCREMENT,");
            sb.Append($"`{DYNAMIC_TABLE_TARG_FIELD}` VARCHAR({DYNAMIC_TABLE_TARG_LEN}) NOT NULL, ");

            // Genereate the column lists
            foreach (DynamicTableField tableField in schema.Fields)
            {
                // Name-Type
                sb.Append($"`{tableField.Name}` {tableField.DataType}");

                // Not Null
                if (tableField.Primary)
                    sb.Append(" NOT NULL");

                // End
                sb.Append(", ");
            }

            // Append primary key
            if (schema.Single)
                sb.Append($"PRIMARY KEY(`{DYNAMIC_TABLE_TARG_FIELD}`)");
            else
            {
                // Use auto-incr seqno as primary key
                sb.Append($"PRIMARY KEY(`{DYNAMIC_TABLE_SEQNO_FIELD}`)");

                // Use target and other primary key as unique index
                sb.Append($", UNIQUE INDEX {DYNAMIC_UNIQUE_INDEX} (`{DYNAMIC_TABLE_TARG_FIELD}`");
                foreach (DynamicTableField tableField in schema.Fields.Where(p => p.Primary))
                    sb.Append($", `{tableField.Name}`");
                sb.Append(")");
            }

            // End the building
            sb.Append(") engine=InnoDB;");
            DbCommand command = GetDbCommand();
            command.CommandText = sb.ToString();
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.Message);
            throw;
        }

        field.Used = true;
        return schema;
    }

    /// <summary>
    /// Preprare the dynamic table for the field
    /// </summary>
    public async Task<List<DynamicTableSchema>> PrepareFieldDataAsync(AppNode node)
    {
        List<DynamicTableSchema> schemaList = new();
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
        AppNode category = await GetAppNodeAsync(field.Category);
        if (category?.RefField == null) return false;
        JObject data = new();
        data.Add(CATEGORY_FIELD_REF_CATE, field.SourceCategory);
        data.Add(CATEGORY_FIELD_REF_TARGET, sourceTarget);
        return await SaveFieldDataAsync(category.RefField, target, data);
    }

    /// <summary>
    /// Sets the ref target of the field
    /// </summary>
    public async Task<bool> SetSourceFieldNode(AppNode category, string target, string sourceCategory, string sourceTarget)
    {
        AppFieldNode field = category.Fields.FirstOrDefault(f => sourceCategory.Equals(f.SourceCategory, StringComparison.OrdinalIgnoreCase));
        return field == null || await SetSourceFieldNode(field, target, sourceTarget);
    }

    /// <summary>
    /// Sets the ref target of the field
    /// </summary>
    public async Task<bool> SetSourceFieldNode(string category, string target, string sourceCategory, string sourceTarget)
    {
        AppNode node = await GetAppNodeAsync(category);
        return node == null || await SetSourceFieldNode(node, target, sourceCategory, sourceTarget);
    }

    /// <summary>
    /// Gets the source field node
    /// </summary>
    public async Task<(AppFieldNode, string)> GetSourceFieldNode(AppFieldNode field, string target, bool forPush = false)
    {
        if (field.SourceNode == null) return (field, target);
        AppNode category = await GetAppNodeAsync(field.Category);

        // Means the category is front only and use the source node's target as target
        if (category?.RefField == null) return forPush ? (null, string.Empty) : await GetSourceFieldNode(field.SourceNode, target);

        JObject query = new();
        query.Add(CATEGORY_FIELD_REF_CATE, field.SourceNode.Category);
        (JToken refdata, _) = await GetFieldDataAsync(category.RefField, target, query, ignoreCache: true);
        if (refdata is JArray { Count: > 0 } arr && arr[0] is JObject jObject && jObject.TryGetValue(CATEGORY_FIELD_REF_TARGET, out JToken val) && val is JValue && !val.IsEmpty())
        {
            string reftarget = val.Value<string>();
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
    public async Task<bool> SaveFieldDataAsync(AppFieldNode field, string target, JToken value = null, bool innerCall = false, bool dropList = false)
    {
        // no front only & enable & no source ref
        if (field.IsFrontEnd || !field.IsEnable || field.SourceNode != null) return false;

        // Not allow the direct data update
        if (!innerCall && !string.IsNullOrWhiteSpace(field.Func)) return false;

        // Prepare
        DynamicTableSchema schema = await PrepareFieldDataAsync(field);
        if (schema == null) return false;

        // Check if incremental updating
        if (field.IsIncrUpdate) dropList = false;

        try
        {
            target = MySqlHelper.EscapeString(target);
            DbCommand command;

            // Save the field data to the target
            if (schema.Single) // Single value
            {
                // Gets the origin value
                (JToken origin, _) = await GetFieldDataAsync(field, target, ignoreCache: true);
                if (schema.IsSameToken(origin, value))
                    return true;

                // Delete if null
                if (value.IsEmpty())
                {
                    if (origin != null)
                    {
                        command = GetDbCommand();
                        command.CommandText = $"DELETE FROM `{schema.Name}` WHERE `{DYNAMIC_TABLE_TARG_FIELD}` = \"{target}\"";
                        await command.ExecuteNonQueryAsync();
                        OnFieldDataChanged(target, field, TransactionChangeOperation.Delete, null, origin);
                    }
                    return true;
                }

                // Check the value type
                if (schema.Fields.Count == 1 && schema.Fields[0].Name == DYNAMIC_TABLE_VALUE_FIELD)
                {
                    // Convert the value
                    string result = schema.Fields[0].ToString(value);
                    bool isInsert = false;

                    // Insert the value
                    if (origin == null)
                    {
                        try
                        {
                            command = GetDbCommand();
                            command.CommandText = schema.Fields[0].IsString
                                ? $"INSERT INTO `{schema.Name}` (`{DYNAMIC_TABLE_TARG_FIELD}`, `{DYNAMIC_TABLE_VALUE_FIELD}`) VALUES ( \"{target}\", \"{MySqlHelper.EscapeString(result!)}\" )"
                                : $"INSERT INTO `{schema.Name}` (`{DYNAMIC_TABLE_TARG_FIELD}`, `{DYNAMIC_TABLE_VALUE_FIELD}`) VALUES ( \"{target}\", {result} )";
                            await command.ExecuteNonQueryAsync();
                            isInsert = true;
                        }
                        catch (MySqlException ex)
                        {
                            if (ex.ErrorCode != MySqlErrorCode.DuplicateKeyEntry)
                                throw;
                        }
                    }

                    // Update the value
                    if (!isInsert)
                    {
                        command = GetDbCommand();
                        command.CommandText = schema.Fields[0].IsString
                            ? $"UPDATE `{schema.Name}` SET `{DYNAMIC_TABLE_VALUE_FIELD}` = \"{MySqlHelper.EscapeString(result!)}\" WHERE `{DYNAMIC_TABLE_TARG_FIELD}` = \"{target}\""
                            : $"UPDATE `{schema.Name}` SET `{DYNAMIC_TABLE_VALUE_FIELD}` = {result!} WHERE `{DYNAMIC_TABLE_TARG_FIELD}` = \"{target}\"";
                        await command.ExecuteNonQueryAsync();
                    }
                    OnFieldDataChanged(target, field, isInsert ? TransactionChangeOperation.Create : TransactionChangeOperation.Modify, value, origin);
                }
                else if (value is JObject pack)
                {
                    // Build the sql
                    StringBuilder sb = new();
                    bool isInsert = false;

                    // Insert
                    if (origin == null)
                    {
                        // Header
                        sb.Append($"INSERT INTO `{schema.Name}` (`{DYNAMIC_TABLE_TARG_FIELD}`, ");
                        schema.AppendFields(sb);
                        sb.Append($") VALUES ( \"{target}\"");

                        // Body
                        foreach ((string _, string val, bool isString, _) in schema.GetFieldValues(pack))
                            sb.Append($",{(val == null ? "null" : (isString ? $"\"{MySqlHelper.EscapeString(val)}\"" : val))}");

                        // Footer
                        sb.Append(");");
                        try
                        {
                            // Execute
                            command = GetDbCommand();
                            command.CommandText = sb.ToString();
                            await command.ExecuteNonQueryAsync();
                            isInsert = true;
                        }
                        catch (MySqlException ex)
                        {
                            if (ex.ErrorCode != MySqlErrorCode.DuplicateKeyEntry)
                                throw;
                        }
                    }

                    // Update
                    if (!isInsert)
                    {
                        // Header
                        sb.Clear();
                        sb.Append($"UPDATE `{schema.Name}` SET ");

                        // Body
                        bool preCond = false;
                        foreach ((string fld, string val, bool isString, _) in schema.GetFieldValues(pack))
                        {
                            sb.Append($"{(preCond ? "," : "")}`{fld}`={(val == null ? "null" : (isString ? $"\"{MySqlHelper.EscapeString(val)}\"" : val))}");
                            preCond = true;
                        }

                        // Footer
                        sb.Append($" WHERE `{DYNAMIC_TABLE_TARG_FIELD}` = \"{target}\"");

                        // Execute
                        command = GetDbCommand();
                        command.CommandText = sb.ToString();
                        await command.ExecuteNonQueryAsync();
                    }
                    OnFieldDataChanged(target, field, isInsert ? TransactionChangeOperation.Create : TransactionChangeOperation.Modify, value, origin);
                }
                else
                {
                    return false;
                }
            }
            else // Multi-Line
            {
                JArray array;
                StringBuilder sb = new();

                // Check if drop list
                if (dropList)
                {
                    // Gets origin data if needed
                    JToken origin = null;
                    if (IsDropDataRequired(field)) (origin, _) = await GetFieldDataAsync(field, target, ignoreCache: true);

                    // drop data
                    command = GetDbCommand();
                    command.CommandText = $"DELETE FROM `{schema.Name}` WHERE `{DYNAMIC_TABLE_TARG_FIELD}` = \"{target}\"";
                    await command.ExecuteNonQueryAsync();
                    OnFieldDataChanged(target, field, TransactionChangeOperation.DropAll, null, origin);
                }

                // Prepare the data
                switch (value)
                {
                    case JArray arr:
                        array = arr;
                        break;
                    case JObject obj:
                        array = new JArray
                        {
                            obj
                        };
                        break;
                    default:
                        return dropList;
                }

                // Foreach
                foreach (JToken val in array)
                {
                    if (val is not JObject pack)
                        continue;

                    // Build where condition
                    bool fullfill = true;
                    sb.Clear();
                    sb.Append($" WHERE `{DYNAMIC_TABLE_TARG_FIELD}` = \"{target}\"");
                    foreach ((string fld, string v, bool isString, _) in schema.GetFieldValues(pack, true))
                    {
                        // Check value
                        if (v == null)
                        {
                            fullfill = false;
                            break;
                        }
                        sb.Append(isString
                            ? $" AND `{fld}` = \"{MySqlHelper.EscapeString(v)}\""
                            : $" AND `{fld}` = {v}"
                        );
                    }
                    if (!fullfill)
                        continue;

                    // Query the origin
                    string where = sb.ToString();
                    JToken origin = null;
                    if (!dropList)
                    {
                        (origin, _) = await GetFieldDataAsync(field, target, pack, ignoreCache: true);
                        if (origin is JArray { Count: > 0 } arr)
                        {
                            origin = arr.First;
                        }
                        else
                        {
                            origin = null;
                        }
                    }
                    if (schema.IsSameToken(origin, pack))
                        continue;

                    // Insert
                    bool isInsert = false;
                    if (origin.IsEmpty())
                    {
                        // Header
                        sb.Clear();
                        sb.Append($"INSERT INTO `{schema.Name}` (`{DYNAMIC_TABLE_TARG_FIELD}`, ");
                        schema.AppendFields(sb);
                        sb.Append($") VALUES ( \"{target}\"");

                        // Body
                        foreach ((string _, string v, bool isString, _) in schema.GetFieldValues(pack))
                            sb.Append($",{(v == null ? "null" : (isString ? $"\"{MySqlHelper.EscapeString(v)}\"" : v))}");

                        // Footer
                        sb.Append(");");
                        try
                        {
                            // Execute
                            command = GetDbCommand();
                            command.CommandText = sb.ToString();
                            await command.ExecuteNonQueryAsync();
                            isInsert = true;
                        }
                        catch (MySqlException ex)
                        {
                            if (ex.ErrorCode != MySqlErrorCode.DuplicateKeyEntry)
                                throw;
                        }
                    }
                    if (!isInsert)
                    {
                        // Header
                        sb.Clear();
                        sb.Append($"UPDATE `{schema.Name}` SET ");

                        // Body
                        bool preCond = false;
                        foreach ((string fld, string v, bool isString, _) in schema.GetFieldValues(pack, false, true))
                        {
                            sb.Append($"{(preCond ? "," : "")}`{fld}`={(v == null ? "null" : (isString ? $"\"{MySqlHelper.EscapeString(v)}\"" : v))}");
                            preCond = true;
                        }

                        // Footer
                        sb.Append(" ");
                        sb.Append(where);

                        // Execute
                        command = GetDbCommand();
                        command.CommandText = sb.ToString();
                        await command.ExecuteNonQueryAsync();
                    }

                    // Register the operation
                    OnFieldDataChanged(target, field, isInsert ? TransactionChangeOperation.Create : TransactionChangeOperation.Modify, pack, origin);
                }
            }
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
    public async Task DeleteFieldListDataAsync(AppFieldNode field, string target, JToken query, bool multi = false, bool innerCall = false)
    {
        // no front only & enable & no source ref
        if (field.IsFrontEnd || !field.IsEnable || field.SourceNode != null) return;

        // Prepare
        target = MySqlHelper.EscapeString(target);
        DynamicTableSchema schema = await PrepareFieldDataAsync(field);

        // Only non-single schema can be used
        if (schema == null || schema.Single) return;
        try
        {
            JArray array;
            StringBuilder sb = new();
            switch (query)
            {
                case JArray arr:
                    array = arr;
                    break;
                case JObject obj:
                    array = new JArray
                    {
                        obj
                    };
                    break;
                default:
                    return;
            }

            // Foreach
            foreach (JToken val in array)
            {
                if (val is not JObject pack)
                    continue;

                // Build where condition
                bool fullfill = true;
                sb.Clear();
                sb.Append($"WHERE `{DYNAMIC_TABLE_TARG_FIELD}` = \"{target}\"");
                foreach ((string fld, string v, bool isString, _) in schema.GetFieldValues(pack, true))
                {
                    // Check value
                    if (v == null)
                    {
                        if (multi)
                            continue;
                        fullfill = false;
                        break;
                    }
                    sb.Append(isString
                        ? $" AND `{fld}` = \"{MySqlHelper.EscapeString(v)}\""
                        : $" AND `{fld}` = {v}"
                    );
                }
                if (!fullfill)
                    continue;

                // Gets the data
                (JToken origin, _) = await GetFieldDataAsync(field, target, pack, ignoreCache: true);
                if (origin is not JArray { Count: > 0 } arr)
                    continue;

                // Delete the data
                DbCommand command = GetDbCommand();
                command.CommandText = $"DELETE FROM `{schema.Name}` {sb}";
                await command.ExecuteNonQueryAsync();
                foreach (JToken token in arr)
                    OnFieldDataChanged(target, field, TransactionChangeOperation.Delete, null, token);
            }
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
        if (field.IsFrontEnd || !field.IsEnable || field.SourceNode != null) return;

        // Prepare
        target = MySqlHelper.EscapeString(target);
        DynamicTableSchema schema = await PrepareFieldDataAsync(field);
        if (schema == null) return;
        try
        {
            if (schema.Single)
            {
                // Gets the deleted data
                (JToken origin, _) = await GetFieldDataAsync(field, target, ignoreCache: true);
                if (origin != null)
                {
                    DbCommand command = GetDbCommand();
                    command.CommandText = $"DELETE FROM `{schema.Name}` WHERE `{DYNAMIC_TABLE_TARG_FIELD}` = \"{target}\";";
                    await command.ExecuteNonQueryAsync();
                    OnFieldDataChanged(target, field, TransactionChangeOperation.Delete, null, origin);
                }
            }
            else
            {
                // Gets origin data if needed
                JToken origin = null;
                if (IsDropDataRequired(field)) (origin, _) = await GetFieldDataAsync(field, target, ignoreCache: true);

                // Drop data
                DbCommand command = GetDbCommand();
                command.CommandText = $"DELETE FROM `{schema.Name}` WHERE `{DYNAMIC_TABLE_TARG_FIELD}` = \"{target}\";";
                await command.ExecuteNonQueryAsync();
                OnFieldDataChanged(target, field, TransactionChangeOperation.DropAll, null, origin);
            }
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
    public async Task<(JToken value, int total)> GetFieldDataAsync(AppFieldNode field, string target, JToken query = null, int? offset = 0, int? count = -1, bool desc = false, bool ignoreCache = false)
    {
        // Front end only
        if (field.IsFrontEnd || !field.IsEnable) return (null, 0);

        // Prepare
        target = MySqlHelper.EscapeString(target);

        (field, target) = await GetSourceFieldNode(field, target);
        if (field == null) return (null, 0);

        DynamicTableSchema schema = await PrepareFieldDataAsync(field);
        if (schema == null)
            return (null, 0);

        string original = Target;
        try
        {
            Target = target;
            if (schema.Single)
            {
                // Gets the data from the cache
                JToken value = !ignoreCache ? await GetFieldDataFromCacheAsync<JToken>(schema.Name, target) : null;
                if (value != null)
                    return (value, 1);

                // Gets the data from the database
                if (schema.Fields.Count == 1 && schema.Fields[0].Name == DYNAMIC_TABLE_VALUE_FIELD)
                {
                    // Single value

                    // Gets the data from data base
                    DbCommand command = GetDbCommand();
                    command.CommandText = $"SELECT `{DYNAMIC_TABLE_VALUE_FIELD}` FROM `{schema.Name}` WHERE `{DYNAMIC_TABLE_TARG_FIELD}` = \"{target}\"";
                    DbDataReader reader = await command.ExecuteReaderAsync();
                    try
                    {
                        if (reader.HasRows)
                        {
                            await reader.ReadAsync();
                            value = schema.Fields[0].FromReader(reader);
                        }
                    }
                    finally
                    {
                        await reader.CloseAsync();
                    }
                }
                else
                {
                    // Struct value

                    // Build sql
                    StringBuilder sbs = new();
                    sbs.Append("SELECT ");
                    schema.AppendFields(sbs);
                    sbs.Append($" From `{schema.Name}`");
                    sbs.Append($" WHERE `{DYNAMIC_TABLE_TARG_FIELD}` = \"{target}\"");

                    // Get datas
                    DbCommand command = GetDbCommand();
                    command.CommandText = sbs.ToString();
                    DbDataReader reader = await command.ExecuteReaderAsync();
                    try
                    {
                        if (reader.HasRows)
                        {
                            await reader.ReadAsync();
                            value = schema.GetFieldPack(this, reader);
                        }
                    }
                    finally
                    {
                        await reader.CloseAsync();
                    }

                    // Generate display only fields
                    await schema.GenerateDisplayOnlyFields(this, value);
                }

                // Save to cache
                if (value != null && !ignoreCache)
                    await SaveFieldDataToCacheAsync(schema.Name, target, value);
                return (value, value == null ? 0 : 1);
            }
            else
            {
                // return list value
                JToken[] cacheValues = null;

                // Build sql
                bool queryTotal = true;
                bool cacheArray = (DataDictConfig?.EnableTotalArrayCache ?? false) && !field.IsIncrUpdate && offset is null or <= 0 && count is null or <= 0;
                Dictionary<string, string> queryPack = null;
                StringBuilder sb = new();
                sb.Append($" From `{schema.Name}`");

                // Conditions
                sb.Append($" WHERE `{DYNAMIC_TABLE_TARG_FIELD}` = \"{target}\"");
                switch (query)
                {
                    // Query based on the conditions
                    case JObject pack:
                        {
                            bool fullfill = true;
                            List<string> primaryKey = new();
                            queryPack = new Dictionary<string, string>();
                            foreach ((string fld, string v, bool isString, bool isList) in schema.GetFieldValues(pack, true))
                            {
                                if (v == null)
                                {
                                    fullfill = false;
                                }
                                else
                                {
                                    // Multi, no cache
                                    if (isList) fullfill = false;

                                    queryPack.Add(fld, v);
                                    primaryKey.Add(v);
                                    sb.Append(
                                        isList
                                            ? $" AND `{fld}` IN {v}"
                                            : isString
                                                ? $" AND `{fld}` = \"{v}\""
                                                : $" AND `{fld}` = {v}");
                                }
                            }

                            // Check count && offset
                            queryTotal = !fullfill;
                            if (fullfill)
                            {
                                // Gets the data by cache
                                JToken v = !ignoreCache ? await GetFieldDataFromCacheAsync<JToken>(schema.Name, target, primaryKey.ToArray()) : null;
                                if (v != null)
                                    return (new JArray
                                {
                                    v
                                }, 1);
                                // Prepare for query
                                cacheValues = new JToken[1];
                            }
                            break;
                        }
                    case JArray array:
                        {
                            if (array.Count == 0)
                                break;
                            queryTotal = false;
                            cacheValues = new JToken[array.Count];
                            bool hasQuery = false;
                            sb.Append(" AND (");

                            // Only allow fullfill query
                            for (int i = 0; i < array.Count; i++)
                            {
                                JToken token = array[i];
                                if (token is not JObject pack)
                                    continue;

                                // Pre
                                StringBuilder subSb = new();
                                bool fullfill = true;
                                bool appenAnd = false;
                                List<string> primaryKey = new();

                                // Build the query
                                if (hasQuery) subSb.Append(" OR ");
                                subSb.Append("(");
                                foreach ((string fld, string v, bool isString, bool isList) in schema.GetFieldValues(pack, true))
                                {
                                    if (isList || v == null)
                                    {
                                        fullfill = false;
                                        break;
                                    }
                                    primaryKey.Add(v);
                                    if (appenAnd) subSb.Append(" AND ");
                                    subSb.Append(isString ? $"`{fld}` = \"{v}\"" : $"`{fld}` = {v}");
                                    appenAnd = true;
                                }
                                subSb.Append(")");

                                // Only allow full query here
                                if (fullfill)
                                {
                                    // Gets the data by cache
                                    JToken v = !ignoreCache ? await GetFieldDataFromCacheAsync<JToken>(schema.Name, target, primaryKey.ToArray()) : null;
                                    if (v == null)
                                    {
                                        sb.Append(subSb.ToString());
                                        hasQuery = true;
                                    }
                                    else
                                    {
                                        cacheValues[i] = v;
                                    }
                                }
                                else
                                {
                                    return (null, 0);
                                }
                            }
                            // Tail
                            sb.Append(")");

                            // Check if all in cache
                            if (cacheValues.All(p => p != null))
                            {
                                JArray result = new();
                                foreach (JToken token in cacheValues) result.Add(token);
                                return (result, result.Count);
                            }
                            if (!hasQuery)
                                return (null, 0);

                            // Continue
                            break;
                        }
                }

                // Query Total
                int total = 0;
                if (queryTotal)
                {
                    // Check the full cache of the target
                    if (cacheArray && !ignoreCache)
                    {
                        JArray cacheResult = await GetFieldDataFromCacheAsync<JArray>(schema.Name, target);
                        if (cacheResult is { Count: > 0 })
                        {
                            // Filter by query pack
                            if (queryPack is { Count: > 0 })
                            {
                                JArray filterArray = new();
                                foreach (JToken token in cacheResult)
                                {
                                    if (token is JObject obj)
                                    {
                                        bool fullMatch = true;
                                        foreach ((string k, string v) in queryPack)
                                        {
                                            if (obj.ContainsKey(k) && obj[k] is JValue { Value: { } } val && v.Equals(val.ToString(CultureInfo.InvariantCulture)))
                                            {
                                                fullMatch = false;
                                                break;
                                            }
                                        }
                                        if (fullMatch)
                                            filterArray.Add(obj);
                                    }
                                }
                                cacheResult = filterArray;
                            }
                            return (cacheResult, cacheResult.Count);
                        }
                    }
                    DbCommand totalCommand = GetDbCommand();
                    totalCommand.CommandText = $"SELECT COUNT(*) {sb};";
                    DbDataReader totalReader = await totalCommand.ExecuteReaderAsync();
                    try
                    {
                        if (totalReader.HasRows && await totalReader.ReadAsync())
                            total = totalReader.GetInt32(0);
                    }
                    finally
                    {
                        await totalReader.CloseAsync();
                    }

                    // Append the rest
                    sb.Append($" ORDER BY `{DYNAMIC_TABLE_SEQNO_FIELD}`");
                    if (desc) sb.Append(" DESC ");
                    if (count is > 0)
                        sb.Append($" LIMIT {count}");
                    if (offset is > 0)
                        sb.Append($" OFFSET {offset}");
                }
                sb.Append(";");

                // Query Data
                StringBuilder header = new();
                header.Append("SELECT ");
                schema.AppendFields(header);
                JArray value = new();
                DbCommand command = GetDbCommand();
                command.CommandText = $"{header}{sb}";
                DbDataReader reader = await command.ExecuteReaderAsync();
                try
                {
                    if (reader.HasRows)
                    {
                        while (await reader.ReadAsync())
                        {
                            JObject pack = schema.GetFieldPack(this, reader);
                            if (pack != null)
                                value.Add(pack);
                        }
                    }
                }
                finally
                {
                    await reader.CloseAsync();
                }

                // Generate display only fields
                foreach (JToken v in value)
                {
                    await schema.GenerateDisplayOnlyFields(this, v);
                }

                // Cache
                switch (queryTotal)
                {
                    // Check if need save the array to cache
                    case true when cacheArray && (queryPack == null || queryPack.Count == 0) && value.Count < 100 && !ignoreCache:
                        await SaveFieldDataToCacheAsync(schema.Name, target, value);
                        break;

                    // Combine the cache data with values
                    case false when cacheValues is { Length: > 0 }:
                        {
                            int i = 0;
                            foreach (JToken token in value)
                            {
                                for (; i < cacheValues.Length; i++)
                                {
                                    if (cacheValues[i] != null) continue;
                                    cacheValues[i] = token;
                                    if (token is JObject obj && !ignoreCache)
                                    {
                                        // Save the data to cache
                                        List<string> primaryKey = new();
                                        foreach ((_, string v, _, _) in schema.GetFieldValues(obj, true))
                                        {
                                            primaryKey.Add(v);
                                        }
                                        await SaveFieldDataToCacheAsync(schema.Name, target, obj, primaryKey.ToArray());
                                    }
                                    break;
                                }
                            }

                            // re-fill the result
                            value.Clear();
                            for (i = 0; i < cacheValues.Length; i++)
                            {
                                if (cacheValues[i] != null)
                                {
                                    value.Add(cacheValues[i]);
                                }
                            }
                            break;
                        }
                }
                return (value, total == 0 ? value.Count : total);
            }
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

    /// <summary>
    /// Gets the field datas
    /// </summary>
    public async Task<Dictionary<string, JToken>> GetFieldDatasAsync(AppFieldNode field, IEnumerable<string> targetList, JToken query = null, int? offset = 0, int? count = -1)
    {
        // Front end only
        if (field.IsFrontEnd || !field.IsEnable || field.SourceNode != null) return new Dictionary<string, JToken>();

        // Prepare
        DynamicTableSchema schema = await PrepareFieldDataAsync(field);
        if (schema == null) return new Dictionary<string, JToken>();

        try
        {
            Dictionary<string, JToken> result = new();

            if (schema.Single)
            {
                // Check the cache first
                List<string> queryTargets = new();
                foreach (string target in targetList.Select(MySqlHelper.EscapeString))
                {
                    JToken value = await GetFieldDataFromCacheAsync<JToken>(schema.Name, target);
                    if (value != null)
                    {
                        result.Add(target, value);
                    }
                    else
                    {
                        queryTargets.Add(target);
                    }
                }
                if (queryTargets.Count == 0)
                {
                    return result;
                }

                // Query the data
                string targets = string.Join(',', queryTargets.Select(p => $"\'{p}\'").ToList());
                if (schema.Fields.Count == 1 && schema.Fields[0].Name == DYNAMIC_TABLE_VALUE_FIELD)
                {
                    // return single value
                    DbCommand command = GetDbCommand();
                    command.CommandText = $"SELECT `{DYNAMIC_TABLE_TARG_FIELD}`, `{DYNAMIC_TABLE_VALUE_FIELD}` FROM `{schema.Name}` WHERE `{DYNAMIC_TABLE_TARG_FIELD}` in ({targets});";
                    DbDataReader reader = await command.ExecuteReaderAsync();
                    try
                    {
                        if (reader.HasRows)
                        {
                            while (await reader.ReadAsync())
                            {
                                string targetId = reader.GetString(DYNAMIC_TABLE_TARG_FIELD);
                                if (!result.ContainsKey(targetId))
                                    result.Add(targetId, schema.Fields[0].FromReader(reader, 1));
                            }
                        }
                    }
                    finally
                    {
                        await reader.CloseAsync();
                    }
                }
                else
                {
                    // return struct value

                    // Build sql
                    StringBuilder sb = new();
                    sb.Append($"SELECT `{DYNAMIC_TABLE_TARG_FIELD}`, ");
                    schema.AppendFields(sb);
                    sb.Append($" From `{schema.Name}`");
                    sb.Append($" WHERE `{DYNAMIC_TABLE_TARG_FIELD}` in ({targets});");

                    // Get datas
                    DbCommand command = GetDbCommand();
                    command.CommandText = sb.ToString();
                    DbDataReader reader = await command.ExecuteReaderAsync();
                    try
                    {
                        if (reader.HasRows)
                        {
                            while (await reader.ReadAsync())
                            {
                                string targetId = reader.GetString(DYNAMIC_TABLE_TARG_FIELD);
                                if (!result.ContainsKey(targetId))
                                    result.Add(targetId, schema.GetFieldPack(this, reader, 1));
                            }
                        }
                    }
                    finally
                    {
                        await reader.CloseAsync();
                    }

                    foreach ((_, JToken v) in result)
                    {
                        // Generate display only fields
                        await schema.GenerateDisplayOnlyFields(this, v);
                    }
                }

                // Save the cache
                foreach (string target in queryTargets.Where(target => result.ContainsKey(target)))
                {
                    await SaveFieldDataToCacheAsync(schema.Name, target, result[target]);
                }
                return result;
            }
            else
            {
                // TODO: Add cache support or not, this is a complex branch won't or rarely used
                targetList = targetList.Select(MySqlHelper.EscapeString).ToList();
                string targets = string.Join(',', targetList.Select(p => $"\'{p}\'").ToList());

                // return list value

                // Build sql
                StringBuilder sb = new();
                sb.Append($"SELECT `{DYNAMIC_TABLE_TARG_FIELD}`, ");
                schema.AppendFields(sb);
                sb.Append($" From `{schema.Name}`");

                // Conditions
                sb.Append($" WHERE `{DYNAMIC_TABLE_TARG_FIELD}` in ({targets})");
                switch (query)
                {
                    // Query based on the conditions
                    case JObject pack:
                        {
                            bool fullfill = true;
                            foreach ((string fld, string v, bool isString, bool isList) in schema.GetFieldValues(pack, true))
                            {
                                if (v == null)
                                {
                                    fullfill = false;
                                }
                                else if (isList)
                                {
                                    fullfill = false;
                                    sb.Append($" AND `{fld}` IN {v}");
                                }
                                else
                                {
                                    sb.Append(isString ? $" AND `{fld}` = \"{v}\"" : $" AND `{fld}` = {v}");
                                }
                            }

                            // Check count && offset
                            if (!fullfill)
                            {
                                sb.Append($" ORDER BY `{DYNAMIC_TABLE_SEQNO_FIELD}`");
                                if (count is > 0)
                                    sb.Append($" LIMIT {count}");
                                if (offset is > 0)
                                    sb.Append($" OFFSET {offset}");
                            }
                            sb.Append(";");
                            break;
                        }
                    case JArray array:
                        bool hasQuery = false;
                        sb.Append(" AND (");

                        // Only allow fullfil query
                        foreach (JToken token in array)
                        {
                            if (token is not JObject pack)
                                continue;

                            // Pre
                            if (hasQuery)
                                sb.Append(" OR ");
                            sb.Append("(");
                            bool fullfill = true;
                            bool appenAnd = false;
                            foreach ((string fld, string v, bool isString, bool isList) in schema.GetFieldValues(pack, true))
                            {
                                if (isList || v == null)
                                {
                                    fullfill = false;
                                    break;
                                }
                                else
                                {
                                    if (appenAnd)
                                        sb.Append(" AND ");
                                    appenAnd = true;
                                    sb.Append(isString
                                        ? $"`{fld}` = \"{v}\""
                                        : $"`{fld}` = {v}");
                                }
                            }

                            // Check count && offset
                            if (fullfill)
                            {
                                hasQuery = true;
                            }
                            else
                            {
                                return null;
                            }

                            // Tail
                            sb.Append(")");
                        }
                        if (!hasQuery)
                            return null;
                        sb.Append($") ORDER BY `{DYNAMIC_TABLE_SEQNO_FIELD}`;");
                        break;
                    default:
                        // Based on offset & count
                        sb.Append($" ORDER BY `{DYNAMIC_TABLE_SEQNO_FIELD}`");
                        if (count is > 0)
                            sb.Append($" LIMIT {count}");
                        if (offset is > 0)
                            sb.Append($" OFFSET {offset}");
                        sb.Append(";");
                        break;
                }

                // Query
                Dictionary<string, JToken> values = null;
                DbCommand command = GetDbCommand();
                command.CommandText = sb.ToString();
                DbDataReader reader = await command.ExecuteReaderAsync();
                try
                {
                    if (reader.HasRows)
                    {
                        values = new Dictionary<string, JToken>();
                        while (await reader.ReadAsync())
                        {
                            string targetId = reader.GetString(DYNAMIC_TABLE_TARG_FIELD);
                            JObject pack = schema.GetFieldPack(this, reader, 1);
                            if (!values.ContainsKey(targetId))
                            {
                                if (pack != null)
                                    values.Add(targetId, new JArray(pack));
                            }
                            else
                            {
                                if (pack != null)
                                    ((JArray)values[targetId]).Add(pack);
                            }
                        }
                    }
                }
                finally
                {
                    await reader.CloseAsync();
                }

                // Generate display only fields
                if (values != null)
                {
                    foreach ((_, JToken v) in values)
                    {
                        if (v is JArray arr)
                        {
                            foreach (JToken val in arr)
                            {
                                await schema.GenerateDisplayOnlyFields(this, val);
                            }
                        }
                    }
                }

                return values;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Gets field data with query condition
    /// </summary>
    public async Task<JArray> GetFieldDataAsync(AppFieldNode field, string target, string condition, JObject query)
    {
        // Front end only
        if (field.IsFrontEnd || !field.IsEnable) return new JArray();

        string original = Target;
        try
        {
            // Prepare
            target = MySqlHelper.EscapeString(target);

            (field, target) = await GetSourceFieldNode(field, target);
            if (field == null) return new JArray();

            DynamicTableSchema schema = await PrepareFieldDataAsync(field);
            if (schema == null) return new JArray();

            Target = target;

            // No condition or single
            if (string.IsNullOrWhiteSpace(condition) || schema.Single)
            {
                (JToken value, _) = await GetFieldDataAsync(field, target);
                return value switch
                {
                    JArray arr => arr,
                    JObject => new JArray
                    {
                        value
                    },
                    JValue { Value: { } } => new JArray
                    {
                        value
                    },
                    _ => new JArray()
                };
            }
            else if (condition.Contains(";"))
            {
                return new JArray();
            }
            else
            {
                // Prepare the condition
                MatchCollection matches = Regex.Matches(condition, @"(\w+)\s*([=><]+|like)\s*\{(\w+)\}", RegexOptions.IgnoreCase);

                // Prepare the replacement
                foreach (Match match in matches)
                {
                    string key = match.Groups[3].Value;
                    DynamicTableField fld = schema.Fields.FirstOrDefault(p => p.Name.Equals(match.Groups[1].Value, StringComparison.InvariantCultureIgnoreCase));
                    if (fld == null || !query.ContainsKey(key)) return new JArray();
                    JToken val = query[key];
                    string replace;
                    if (fld.Type == DynamicTableFieldType.DateTime)
                    {
                        DateTime dt = val.ToObject<DateTime>();
                        if (match.Groups[2].Value.Contains(">"))
                        {
                            dt = dt.GetFirstTimeOfDay();
                        }
                        else if (match.Groups[2].Value.Contains("<"))
                        {
                            dt = dt.GetLastTimeOfDay();
                        }
                        replace = $"\"{dt:yyyy-MM-dd HH:mm:ss}\"";
                    }
                    else
                    {
                        replace = fld.ToString(val);
                        if (fld.IsString) replace = $"\"{replace}\"";
                    }
                    condition = condition.Replace($"{{{key}}}", replace);
                }

                // Build the query sql
                StringBuilder sb = new();
                sb.Append($" From `{schema.Name}`");

                // Conditions
                sb.Append($" WHERE `{DYNAMIC_TABLE_TARG_FIELD}` = \"{target}\" AND {condition};");

                // Query Data
                StringBuilder header = new();
                header.Append("SELECT ");
                schema.AppendFields(header);
                JArray value = new();
                DbCommand command = GetDbCommand();
                command.CommandText = $"{header}{sb}";
                DbDataReader reader = await command.ExecuteReaderAsync();
                try
                {
                    if (reader.HasRows)
                    {
                        while (await reader.ReadAsync())
                        {
                            JObject pack = schema.GetFieldPack(this, reader);
                            if (pack != null)
                                value.Add(pack);
                        }
                    }
                }
                finally
                {
                    await reader.CloseAsync();
                }

                // Generate display only fields
                foreach (JToken val in value)
                {
                    await schema.GenerateDisplayOnlyFields(this, val);
                }

                return value;
            }
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

    /// <summary>
    /// Reload push field data
    /// </summary>
    public async Task<(JToken value, int total)> ReLoadFieldDataAsync(AppFieldNode field, string target, JToken query = null, int? offset = 0, int? count = -1, bool desc = false, bool ignoreCache = false)
    {
        if (field.FuncNode != null)
        {
            await BeginTransactionAsync();

            // Process data field push
            await ProcessDataPush(target, new TransactionChangeData(), true, false, field);

            // Clear caches
            foreach ((string t, TransactionChangeData changeData) in transChangedData)
                await ClearCacheByChangedData(t, changeData);

            // commit transactions
            if (transaction != null)
            {
                await transaction.CommitAsync();
                await transaction.DisposeAsync();
                transaction = null;
            }

            // Clear the caches
            if (clearMessage != null)
            {
                try
                {
                    foreach (string key in clearMessage.Keys)
                    {
                        await Cache.Delete(key);
                    }
                    foreach (string key in clearMessage.HashKeys)
                    {
                        await HashCache.Delete(key);
                    }
                    foreach ((string key, List<string> fields) in clearMessage.HashFields)
                    {
                        foreach (string f in fields)
                        {
                            await HashCache.DeleteHashItem<JToken>(key, f);
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (MessageQueue != null)
                    {
                        await MessageQueue.SendAsync(clearMessage, "CLEAR");
                    }
                    Logger.LogError(ex.Message);
                }
                clearMessage = null;
            }
        }
        // Return changes
        return await GetFieldDataAsync(field, target, query, offset, count, desc, ignoreCache);
    }

    #endregion

    #region Dynamic Data Cache

    // Get data from cache
    async Task<T> GetFieldDataFromCacheAsync<T>(string table, string target, params string[] keys) where T : JToken
    {
        if (DataDictConfig is not { DynamicCacheInterval: > 0 }) return default;
        try
        {
            if (keys is { Length: > 0 })
            {
                // Hash
                if ((await HashCache.TryGetHashItem<JToken>($"{DYNAMIC_HASH_CACHE_PREFIX}{table}:{target}", string.Join(':', keys))).Out(out JToken value))
                {
                    if (value is T v)
                        return v;
                }
            }
            else
            {
                // Single
                if ((await Cache.TryGet<JToken>($"{DYNAMIC_SINGLE_CACHE_PREFIX}{table}:{target}")).Out(out JToken value))
                {
                    if (value is T v)
                        return v;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"{nameof(GetFieldDataFromCacheAsync)} Failed");
        }
        return null;
    }

    // Delete data in cache
    void DeleteFieldDataInCacheAsync(string table, string target, params string[] keys)
    {
        if (DataDictConfig is not { DynamicCacheInterval: > 0 }) return;
        try
        {
            // DELETE SINGLE
            string key = $"{DYNAMIC_SINGLE_CACHE_PREFIX}{table}:{target}";
            if (clearMessage.Keys != null && !clearMessage.Keys.Contains(key))
                clearMessage.Keys.Add(key);

            // DELETE Hash
            key = $"{DYNAMIC_HASH_CACHE_PREFIX}{table}:{target}";
            if (keys is { Length: > 0 })
            {
                // Delete by field
                string field = string.Join(':', keys);
                if (clearMessage.HashFields != null)
                {
                    if (clearMessage.HashFields.ContainsKey(key))
                    {
                        clearMessage.HashFields[key].Add(field);
                    }
                    else
                    {
                        clearMessage.HashFields.Add(key, new List<string>
                        {
                            field
                        });
                    }
                }
            }
            else if (clearMessage.HashKeys != null && !clearMessage.HashKeys.Contains(key))
            {
                // Delete all
                clearMessage.HashKeys.Add(key);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"{nameof(DeleteFieldDataInCacheAsync)} Failed");
        }
    }

    // Save data to cache
    async Task SaveFieldDataToCacheAsync(string table, string target, JToken value, params string[] keys)
    {
        if (DataDictConfig is not { DynamicCacheInterval: > 0 }) return;
        try
        {
            if (keys is { Length: > 0 })
            {
                // Hash
                string key = $"{DYNAMIC_HASH_CACHE_PREFIX}{table}:{target}";
                await HashCache.SetHashItem(key, string.Join(':', keys), value);
                await HashCache.SetExpireTime(key, TimeSpan.FromSeconds(DataDictConfig.DynamicCacheInterval));
            }
            else
            {
                // Single
                await Cache.Set($"{DYNAMIC_SINGLE_CACHE_PREFIX}{table}:{target}", value, TimeSpan.FromSeconds(DataDictConfig.DynamicCacheInterval));
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"{nameof(SaveFieldDataToCacheAsync)} Failed");
        }
    }

    // Clear by changed data
    async Task ClearCacheByChangedData(string target, TransactionChangeData changeData)
    {
        foreach ((AppFieldNode field, List<FieldDataChangeData> changes) in changeData.Changes)
        {
            DynamicTableSchema schema = await PrepareFieldDataAsync(field);
            if (schema == null) continue;
            if (schema.Single)
            {
                DeleteFieldDataInCacheAsync(schema.Name, target);
            }
            else
            {
                if (changes.Any(p => p.Operation == TransactionChangeOperation.DropAll))
                {
                    // Delete all
                    DeleteFieldDataInCacheAsync(schema.Name, target);
                }
                else
                {
                    bool fullDel = false;
                    foreach (FieldDataChangeData data in changes)
                    {
                        if (data.Origin == null) continue;
                        List<string> primaryKey = new();
                        if (data.Origin is not JObject pack)
                        {
                            fullDel = true;
                            break;
                        }
                        foreach ((_, string v, _, _) in schema.GetFieldValues(pack, true))
                        {
                            if (v == null)
                            {
                                fullDel = true;
                                break;
                            }
                            else
                            {
                                primaryKey.Add(v);
                            }
                        }
                        if (fullDel) break;
                        DeleteFieldDataInCacheAsync(schema.Name, target, primaryKey.ToArray());
                    }
                    if (fullDel)
                        DeleteFieldDataInCacheAsync(schema.Name, target);
                }
            }
        }
    }

    #endregion

    #region Transaction

    /// <summary>
    /// Begin transaction.
    /// </summary>
    public async Task BeginTransactionAsync()
    {
        if (MySql.Context.Database.GetDbConnection().State == ConnectionState.Closed)
            await MySql.Context.Database.GetDbConnection().OpenAsync();

        // Begin transaction
        if (MySql.Context.Database.CurrentTransaction == null)
            transaction = await MySql.Context.Database.BeginTransactionAsync();
        else
            transaction = null;
        transChangedData.Clear();
        clearMessage = new DotNetGaiaCloudCacheClearMessage
        {
            Keys = new List<string>(),
            HashKeys = new List<string>(),
            HashFields = new Dictionary<string, List<string>>()
        };
    }

    /// <summary>
    /// Commit transaction.
    /// </summary>
    public async Task<Dictionary<string, Dictionary<string, List<FieldDataChangeData>>>> CommitAsync(bool pushAll = false, bool pushAllFields = false)
    {
        // Gather change datas
        Dictionary<string, Dictionary<string, List<FieldDataChangeData>>> commits = new();

        // Process data field push
        foreach (string target in transChangedData.Keys.ToArray())
        {
            // Gather change datas
            Dictionary<string, List<FieldDataChangeData>> commitFields = new();
            foreach ((AppFieldNode field, List<FieldDataChangeData> data) in transChangedData[target].Changes)
                commitFields[field.Name] = data;
            commits[target] = commitFields;

            // process data push
            await ProcessDataPush(target, transChangedData[target], pushAll, pushAllFields);
        }

        // Clear caches
        foreach ((string target, TransactionChangeData changeData) in transChangedData)
            await ClearCacheByChangedData(target, changeData);

        // commit transactions
        if (transaction != null)
        {
            await transaction.CommitAsync();
            await transaction.DisposeAsync();
            transaction = null;
        }

        // Clear the caches
        if (clearMessage != null)
        {
            try
            {
                foreach (string key in clearMessage.Keys)
                {
                    await Cache.Delete(key);
                }
                foreach (string key in clearMessage.HashKeys)
                {
                    await HashCache.Delete(key);
                }
                foreach ((string key, List<string> fields) in clearMessage.HashFields)
                {
                    foreach (string field in fields)
                    {
                        await HashCache.DeleteHashItem<JToken>(key, field);
                    }
                }
            }
            catch (Exception ex)
            {
                if (MessageQueue != null)
                {
                    await MessageQueue.SendAsync(clearMessage, "CLEAR");
                }
                Logger.LogError(ex.Message);
            }
            clearMessage = null;
        }

        // Return changes
        return commits;
    }

    /// <summary>
    /// Rollback transaction.
    /// </summary>
    public async Task RollbackAsync()
    {
        if (transaction != null)
            await transaction.RollbackAsync();
        transaction = null;
    }

    // Process the data push
    async Task ProcessDataPush(string target, TransactionChangeData changeData, bool pushAll = false, bool pushAllFields = false, AppFieldNode pushNode = null)
    {
        // record the target
        Target = target;

        // Build the push generation
        List<AppFieldNode> baseFields = changeData.Changes.Keys.Where(p => p.Observers is { Count: > 0 }).ToList();

        // If push all
        if (pushAllFields)
        {
            baseFields.Clear();
            foreach (string category in changeData.Changes.Keys.Select(p => p.Category).Distinct())
            {
                AppNode AppNode = await GetAppNodeAsync(category);
                if (AppNode != null)
                {
                    baseFields.AddRange(AppNode.Fields.Where(f => f.FuncNode == null && f.Observers is { Count: > 0 }));
                }
            }
        }

        // Generate the push levels
        FieldDataPushLevel root = null;
        FieldDataPushLevel curr = null;
        Dictionary<Guid, FieldDataPushLevel> updateFieldsLvlMap = new();

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
            foreach (AppFieldNode node in baseFields.SelectMany(p => p.Observers).Distinct().Where(n => n.IsEnable && !n.IsFrontEnd))
            {
                if (!updateFieldsLvlMap.ContainsKey(node.Id))
                {
                    next.Fields.Add(node);
                    updateFieldsLvlMap.Add(node.Id, next);
                }
                else
                {
                    // Move the field to current
                    AppFieldNode item = updateFieldsLvlMap[node.Id].Fields.First(p => p.Id == node.Id);
                    next.Fields.Add(item);
                    updateFieldsLvlMap[node.Id].Fields.Remove(item);
                    updateFieldsLvlMap[node.Id] = next;
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
        Dictionary<AppFieldNode, JToken> otherFields = new();
        HashSet<AppFieldNode> displayOnlyGens = new();
        HashSet<string> otherTargets = new();
        while (root?.Fields.Count is > 0)
        {
            foreach (AppFieldNode field in root.Fields)
            {
                // Check ref
                AppFieldNode tarField = field;
                string realTarget = target;
                if (field.SourceNode != null)
                {
                    (tarField, realTarget) = await GetSourceFieldNode(field, target, true);
                    if (tarField == null) continue;
                    if (realTarget != target) otherTargets.Add(realTarget);
                }

                // Prepare arguments
                FieldDataPushArg[] args = new FieldDataPushArg[field.FuncCategoryArgs.Count];
                FunctionNode funcNode = field.FuncNode;
                int arrayIndex = -1;
                for (int i = 0; i < field.FuncCategoryArgs.Count; i++)
                {
                    AppFieldNodeArgument call = field.FuncCategoryArgs[i];
                    args[i] = new FieldDataPushArg();

                    // Generate argument
                    List<FieldDataChangeData> changes = (!pushAll || field.SourceNode != null) && changeData.Changes.ContainsKey(call.CategoryField) ? changeData.Changes[call.CategoryField] : null;
                    args[i].Type = call.CategoryField.TypeNode;
                    if (args[i].Type is ArrayNode && (funcNode.Args[i].TypeNode is not ArrayNode || arrayIndex < 0)) arrayIndex = i;

                    // Check changes
                    if (changes == null)
                    {
                        args[i].IsFull = true;
                        args[i].Changed = false;

                        // full data
                        if (otherFields.ContainsKey(call.CategoryField))
                        {
                            args[i].Value = otherFields[call.CategoryField];
                        }
                        else
                        {
                            (args[i].Value, _) = await GetFieldDataAsync(call.CategoryField, target, ignoreCache: true);
                            otherFields[call.CategoryField] = args[i].Value ?? new JValue((JToken)null);
                        }
                        args[i].Origin = args[i].Value;
                    }
                    else
                    {
                        // generate display only fields for upload datas
                        if (displayOnlyGens.Add(call.CategoryField))
                        {
                            // check schema
                            if (call.CategoryField.TypeNode is ArrayNode { BaseNode: StructNode } or StructNode)
                            {
                                DynamicTableSchema schema = await PrepareFieldDataAsync(call.CategoryField);
                                foreach (FieldDataChangeData change in changes)
                                {
                                    // for new
                                    if (change.Value is JArray varr)
                                    {
                                        foreach (JToken token in varr)
                                        {
                                            if (token is JObject obj && !obj.IsEmpty())
                                            {
                                                await schema.GenerateDisplayOnlyFields(this, obj);
                                            }
                                        }
                                    }
                                    else if (change.Value is JObject vobj && !vobj.IsEmpty())
                                    {
                                        await schema.GenerateDisplayOnlyFields(this, vobj);
                                    }

                                    // for origin
                                    if (change.Origin is JArray oarr)
                                    {
                                        foreach (JToken token in oarr)
                                        {
                                            if (token is JObject obj && !obj.IsEmpty())
                                            {
                                                await schema.GenerateDisplayOnlyFields(this, obj);
                                            }
                                        }
                                    }
                                    else if (change.Origin is JObject gobj && !gobj.IsEmpty())
                                    {
                                        await schema.GenerateDisplayOnlyFields(this, gobj);
                                    }
                                }
                            }
                        }

                        args[i].Changed = true;
                        if (call.CategoryField.TypeNode is ArrayNode)
                        {
                            // Check array if need part update
                            JArray values = new();
                            JArray origins = new();
                            foreach (FieldDataChangeData change in changes)
                            {
                                switch (change.Operation)
                                {
                                    case TransactionChangeOperation.Create:
                                        if (!change.Value.IsEmpty())
                                        {
                                            if (change.Value is JArray varr)
                                            {
                                                //  For array without primary keys
                                                args[i].IsFull = true;
                                                values = varr;
                                            }
                                            else
                                            {
                                                values.Add(change.Value);
                                            }
                                        }
                                        break;
                                    case TransactionChangeOperation.Modify:
                                        if (!change.Value.IsEmpty())
                                        {
                                            if (change.Value is JArray varr)
                                            {
                                                //  For array without primary keys
                                                args[i].IsFull = true;
                                                values = varr;
                                            }
                                            else
                                            {
                                                values.Add(change.Value);
                                            }
                                        }
                                        if (!change.Origin.IsEmpty())
                                        {
                                            if (change.Origin is JArray varr)
                                            {
                                                //  For array without primary keys
                                                args[i].IsFull = true;
                                                origins = varr;
                                            }
                                            else
                                            {
                                                origins.Add(change.Origin);
                                            }
                                        }
                                        break;
                                    case TransactionChangeOperation.Delete:
                                        if (!change.Origin.IsEmpty())
                                        {
                                            if (change.Origin is JArray varr)
                                            {
                                                //  For array without primary keys
                                                args[i].IsFull = true;
                                                origins = varr;
                                            }
                                            else
                                            {
                                                origins.Add(change.Origin);
                                            }
                                        }
                                        break;
                                    case TransactionChangeOperation.DropAll:
                                        args[i].IsFull = true;
                                        if (change.Origin is JArray arr)
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
                        else if (args[i].Type is ArrayNode { BaseNode: StructNode })
                        {
                            // Gets the value
                            if (args[i].Value is JArray arr)
                            {
                                for (int h = 0; h < arr.Count; h++)
                                {
                                    arr[h] = arr[h].GetValueByPaths(call.DataField);
                                }
                            }

                            // Gets the origin
                            if (args[i].Origin is JArray oarr)
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
                    AppFieldNodeArgument call = field.FuncCategoryArgs[arrayIndex];

                    // full data
                    if (otherFields.ContainsKey(call.CategoryField))
                    {
                        arg.Value = otherFields[call.CategoryField];
                    }
                    else
                    {
                        (arg.Value, _) = await GetFieldDataAsync(call.CategoryField, target, ignoreCache: true);
                        otherFields[call.CategoryField] = arg.Value ?? new JValue((JToken)null);
                    }
                    arg.Origin = arg.Value;
                    arg.IsFull = true;
                }

                // If part update or is ref, must get the original calc result
                JToken oldResult = null;
                if (arrayIndex >= 0 && (!args[arrayIndex].IsFull || field.SourceNode != null))
                {
                    JArray originCall = new();
                    foreach (FieldDataPushArg arg in args)
                        originCall.Add(arg.Origin);

                    // Check if use element
                    if (funcNode.Args[arrayIndex].TypeNode is not ArrayNode)
                    {
                        JArray resultArr = new();
                        foreach (JToken t in (JArray)args[arrayIndex].Origin)
                        {
                            originCall[arrayIndex] = t;
                            JToken calcRes = await CallFunction(field.Func, originCall, !string.IsNullOrWhiteSpace(funcNode.RetType) ? funcNode.RetType : field.Type);
                            if (calcRes is JArray arr)
                            {
                                foreach (JToken ele in arr)
                                {
                                    if (!ele.IsEmpty())
                                        resultArr.Add(ele);
                                }
                            }
                            else if (!calcRes.IsEmpty())
                            {
                                resultArr.Add(calcRes);
                            }
                        }
                        oldResult = resultArr;
                    }
                    else
                    {
                        oldResult = await CallFunction(field.Func, originCall, !string.IsNullOrWhiteSpace(funcNode.RetType) ? funcNode.RetType : field.Type);
                    }
                }

                // Calc the new result
                JToken newResult;
                JArray callArgs = new();
                foreach (FieldDataPushArg arg in args)
                    callArgs.Add(arg.Value);

                // Check if use element
                if (arrayIndex >= 0 && funcNode.Args[arrayIndex].TypeNode is not ArrayNode)
                {
                    JArray resultArr = new();
                    foreach (JToken t in (JArray)args[arrayIndex].Value)
                    {
                        callArgs[arrayIndex] = t;
                        JToken calcRes = await CallFunction(field.Func, callArgs, !string.IsNullOrWhiteSpace(funcNode.RetType) ? funcNode.RetType : field.Type);
                        if (calcRes is JArray arr)
                        {
                            foreach (JToken ele in arr)
                            {
                                if (!ele.IsEmpty())
                                    resultArr.Add(ele);
                            }
                        }
                        else if (!calcRes.IsEmpty())
                        {
                            resultArr.Add(calcRes);
                        }
                    }
                    newResult = resultArr;
                }
                else
                {
                    newResult = await CallFunction(field.Func, callArgs, !string.IsNullOrWhiteSpace(funcNode.RetType) ? funcNode.RetType : field.Type);
                }

                // Join the result
                JToken result = null;
                switch (field.TypeNode)
                {
                    case EnumNode @enum:
                        {
                            // Can't join the result, only directly assignment allowed
                            if (newResult is JValue)
                            {
                                (JToken res, JToken error) = await @enum.ValidateValue(this, newResult);
                                result = error.IsEmpty() ? res : throw new Exception(error.ToString());
                            }
                            break;
                        }
                    case ScalarNode scalar:
                        {
                            // Gets the join method
                            ArrayJoinMethod method = scalar.IsNumber ? ArrayJoinMethod.Sum : ArrayJoinMethod.Assign;
                            if (field.JoinMethods is JValue val && !val.IsEmpty() && Enum.TryParse(typeof(ArrayJoinMethod), val.ToString(CultureInfo.InvariantCulture), out object m) && m != null)
                                method = (ArrayJoinMethod)m;
                            if (arrayIndex < 0 || args[arrayIndex].IsFull)
                            {
                                // Full
                                result = await GroupJoin(scalar, newResult, method);
                            }
                            else
                            {
                                // Part
                                (JToken origin, _) = await GetFieldDataAsync(tarField, realTarget);
                                JValue old = await GroupJoin(scalar, oldResult, method);
                                JValue now = await GroupJoin(scalar, newResult, method);

                                // Update with join method
                                switch (method)
                                {
                                    case ArrayJoinMethod.Assign:
                                        {
                                            result = now;
                                            break;
                                        }
                                    case ArrayJoinMethod.Distinct:
                                        {
                                            result = origin.IsEmpty() ? now : origin;
                                            break;
                                        }
                                    case ArrayJoinMethod.Sum:
                                        {
                                            if (scalar.IsInt)
                                            {
                                                result = (!origin.IsEmpty() ? ((JValue)origin).Value<int>() : 0)
                                                         + (!now.IsEmpty() ? (now).Value<int>() : 0)
                                                         - (!old.IsEmpty() ? (old).Value<int>() : 0);
                                            }
                                            else if (scalar.IsSingle)
                                            {
                                                result = (!origin.IsEmpty() ? ((JValue)origin).Value<float>() : 0)
                                                         + (!now.IsEmpty() ? (now).Value<float>() : 0)
                                                         - (!old.IsEmpty() ? (old).Value<float>() : 0);
                                            }
                                            else
                                            {
                                                result = (!origin.IsEmpty() ? ((JValue)origin).Value<decimal>() : 0)
                                                         + (!now.IsEmpty() ? (now).Value<decimal>() : 0)
                                                         - (!old.IsEmpty() ? (old).Value<decimal>() : 0);
                                            }
                                        }
                                        break;
                                    case ArrayJoinMethod.Count:
                                        {
                                            result = (!origin.IsEmpty() ? ((JValue)origin).Value<int>() : 0)
                                                     + (!now.IsEmpty() ? (now).Value<int>() : 0)
                                                     - (!old.IsEmpty() ? (old).Value<int>() : 0);
                                            break;
                                        }
                                    case ArrayJoinMethod.Average:
                                    case ArrayJoinMethod.Min:
                                    case ArrayJoinMethod.Max:
                                        {
                                            result = null;
                                            break;
                                        }
                                    default:
                                        throw new ArgumentOutOfRangeException();
                                }
                            }
                            break;
                        }
                    case StructNode { Fields.Count: > 0 } @struct:
                        {
                            // Gets the join method map
                            Dictionary<string, ArrayJoinMethod> joinMethodMap = new();

                            // Default join
                            foreach (StructNodeField f in @struct.Fields)
                            {
                                if (f.TypeNode is ScalarNode s)
                                    joinMethodMap[f.Name] = s.IsNumber ? ArrayJoinMethod.Sum : ArrayJoinMethod.Assign;
                            }

                            // Check settings
                            if (field.JoinMethods is JObject map)
                            {
                                foreach ((string name, JToken token) in map)
                                    if (token is JValue val && !val.IsEmpty() && Enum.TryParse(typeof(ArrayJoinMethod), val.ToString(CultureInfo.InvariantCulture), out object m) && m != null)
                                        joinMethodMap[name] = (ArrayJoinMethod)m;
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
                                (JToken origin, _) = await GetFieldDataAsync(tarField, realTarget);
                                JObject old = await GroupJoin(@struct, oldResult, joinMethodMap);
                                JObject now = await GroupJoin(@struct, newResult, joinMethodMap);

                                // Update with join method
                                if (origin.IsEmpty() && old.IsEmpty())
                                {
                                    result = now;
                                }
                                else
                                {
                                    JObject final = !origin.IsEmpty() ? (JObject)origin.DeepClone() : new JObject();
                                    foreach (StructNodeField nodeField in @struct.Fields)
                                    {
                                        switch (joinMethodMap.ContainsKey(nodeField.Name) ? joinMethodMap[nodeField.Name] : ArrayJoinMethod.Assign)
                                        {
                                            case ArrayJoinMethod.Assign:
                                                {
                                                    if (!now.IsEmpty() && now.ContainsKey(nodeField.Name))
                                                        final[nodeField.Name] = now[nodeField.Name];
                                                    //else if (final.ContainsKey(nodeField.Name))
                                                    //    final.Remove(nodeField.Name);
                                                    break;
                                                }
                                            case ArrayJoinMethod.Distinct:
                                                {
                                                    if (origin.IsEmpty() && !now.IsEmpty() && now.ContainsKey(nodeField.Name))
                                                        final[nodeField.Name] = now[nodeField.Name];
                                                    break;
                                                }
                                            case ArrayJoinMethod.Sum when nodeField.TypeNode is ScalarNode { IsNumber: true }:
                                                {
                                                    decimal sum = 0m;
                                                    if (origin is JObject originObj && originObj.ContainsKey(nodeField.Name) && originObj[nodeField.Name] is JValue oval && !oval.IsEmpty())
                                                        sum = oval.Value<decimal>();
                                                    if (!old.IsEmpty() && old.ContainsKey(nodeField.Name) && old[nodeField.Name] is JValue olval && !olval.IsEmpty())
                                                        sum -= olval.Value<decimal>();
                                                    if (!now.IsEmpty() && now.ContainsKey(nodeField.Name) && now[nodeField.Name] is JValue nval && !nval.IsEmpty())
                                                        sum += nval.Value<decimal>();
                                                    final[nodeField.Name] = sum;
                                                    break;
                                                }
                                            case ArrayJoinMethod.Count when nodeField.TypeNode is ScalarNode { IsNumber: true }:
                                                {
                                                    int sum = 0;
                                                    if (origin is JObject originObj && originObj.ContainsKey(nodeField.Name) && originObj[nodeField.Name] is JValue oval && !oval.IsEmpty())
                                                        sum = oval.Value<int>();
                                                    if (!old.IsEmpty() && old.ContainsKey(nodeField.Name) && old[nodeField.Name] is JValue olval && !olval.IsEmpty())
                                                        sum -= olval.Value<int>();
                                                    if (!now.IsEmpty() && now.ContainsKey(nodeField.Name) && now[nodeField.Name] is JValue nval && !nval.IsEmpty())
                                                        sum += nval.Value<int>();
                                                    final[nodeField.Name] = sum;
                                                    break;
                                                }
                                            case ArrayJoinMethod.Average:
                                            case ArrayJoinMethod.Min:
                                            case ArrayJoinMethod.Max:
                                                {
                                                    final[nodeField.Name] = null;
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
                    case ArrayNode { BaseNode: EnumNode or ScalarNode } array:
                        {
                            // simple array, use the new result directly, can't join the data, normally this case won't be really used.
                            // There is no primay key to track which should be removed, which should be updated
                            if (newResult is JArray { Count: > 0 } arr)
                            {
                                (JToken res, JToken error) = await array.ValidateValue(this, arr);
                                result = error.IsEmpty() ? res : throw new Exception(error.ToString());

                                // Distinct
                                JValue val = field.JoinMethods is JValue fval && !fval.IsEmpty() ? fval : null;
                                if (!val.IsEmpty() && Enum.TryParse(typeof(ArrayJoinMethod), val.ToString(CultureInfo.InvariantCulture), out object m)
                                    && m != null && (ArrayJoinMethod)m == ArrayJoinMethod.Distinct)
                                {
                                    JArray newArr = new();
                                    foreach (JToken t in result.Distinct())
                                        newArr.Add(t);
                                    result = newArr;
                                }
                            }
                            break;
                        }
                    case ArrayNode { BaseNode: StructNode { Fields: { Count: > 0 } } structNode, Primary: { Count: > 0 } } array:
                        {
                            // Gets the join method map
                            Dictionary<string, ArrayJoinMethod> joinMethodMap = new();
                            Dictionary<string, JObject> resultMap;
                            bool isFull = arrayIndex < 0 || args[arrayIndex].IsFull;

                            // Gets the value fields
                            List<string> valueFields = new();
                            Dictionary<string, NamespaceNode> primaryNodes = new();
                            foreach (StructNodeField fieldType in structNode.Fields)
                            {
                                if (!array.Primary.Contains(fieldType.Name))
                                {
                                    valueFields.Add(fieldType.Name);

                                    if (fieldType.TypeNode is ScalarNode s)
                                    {
                                        joinMethodMap[fieldType.Name] = s.IsNumber ? ArrayJoinMethod.Sum : ArrayJoinMethod.Assign;
                                    }
                                }
                                else
                                    primaryNodes.Add(fieldType.Name, fieldType.TypeNode);
                            }

                            // Based on field join methods
                            if (field.JoinMethods is JObject map)
                            {
                                foreach ((string name, JToken token) in map)
                                    if (token is JValue val && !val.IsEmpty() && Enum.TryParse(typeof(ArrayJoinMethod), val.ToString(CultureInfo.InvariantCulture), out object m) && m != null && !(!isFull && m.Equals(ArrayJoinMethod.Average)))
                                        joinMethodMap[name] = (ArrayJoinMethod)m;
                            }
                            // Based on array join methods
                            else if (array.JoinMethods != null)
                            {
                                foreach ((string name, ArrayJoinMethod token) in array.JoinMethods)
                                    if (!(!isFull && token.Equals(ArrayJoinMethod.Average)))
                                        joinMethodMap[name] = token;
                            }

                            // Generate result map
                            if (isFull)
                            {
                                // Full
                                resultMap = await GroupJoinObjectMap(array, newResult, joinMethodMap);
                            }
                            else
                            {
                                // Part

                                // Group join the old & now data
                                Dictionary<string, JObject> oldMap = await GroupJoinObjectMap(array, oldResult, joinMethodMap);
                                Dictionary<string, JObject> nowMap = await GroupJoinObjectMap(array, newResult, joinMethodMap);

                                // Query the original data
                                HashSet<string> keys = new();
                                JArray query = new();
                                foreach ((string key, JObject obj) in oldMap)
                                {
                                    if (keys.Contains(key)) continue;
                                    keys.Add(key);
                                    query.Add(obj);
                                }
                                foreach ((string key, JObject obj) in nowMap)
                                {
                                    if (keys.Contains(key)) continue;
                                    keys.Add(key);
                                    query.Add(obj);
                                }

                                // Gets the original data
                                resultMap = new Dictionary<string, JObject>();
                                if (!query.IsEmpty())
                                {
                                    (JToken value, _) = await GetFieldDataAsync(tarField, realTarget, query, ignoreCache: true);
                                    if (value is JArray arr)
                                    {
                                        foreach (JToken token in arr)
                                        {
                                            if (token is not JObject obj) continue;
                                            string key = array.GetPrimaryKey(obj);
                                            if (string.IsNullOrWhiteSpace(key)) continue;
                                            resultMap[key] = obj;
                                        }
                                    }
                                }

                                // Generate the result map
                                foreach (string key in keys)
                                {
                                    if (resultMap.ContainsKey(key))
                                    {
                                        JObject res = resultMap[key];
                                        JObject old = oldMap.ContainsKey(key) ? oldMap[key] : null;
                                        JObject now = nowMap.ContainsKey(key) ? nowMap[key] : null;
                                        foreach (string s in valueFields)
                                        {
                                            switch (joinMethodMap.ContainsKey(s) ? joinMethodMap[s] : ArrayJoinMethod.Assign)
                                            {
                                                case ArrayJoinMethod.Assign:
                                                    if (!now.IsEmpty() && now.ContainsKey(s))
                                                        res[s] = now[s];
                                                    //else if (res.ContainsKey(s))
                                                    //    res.Remove(s);
                                                    break;
                                                case ArrayJoinMethod.Distinct:
                                                    break;
                                                case ArrayJoinMethod.Sum:
                                                    res[s] = (res.ContainsKey(s) && !res[s].IsEmpty() ? res[s]!.Value<decimal>() : 0) +
                                                             (!now.IsEmpty() && now.ContainsKey(s) && !now[s].IsEmpty() ? now[s]!.Value<decimal>() : 0) -
                                                             (!old.IsEmpty() && old.ContainsKey(s) && !old[s].IsEmpty() ? old[s]!.Value<decimal>() : 0);
                                                    break;
                                                case ArrayJoinMethod.Count:
                                                    res[s] = (res.ContainsKey(s) && !res[s].IsEmpty() ? res[s]!.Value<int>() : 0) +
                                                             (!now.IsEmpty() && now.ContainsKey(s) && !now[s].IsEmpty() ? now[s]!.Value<int>() : 0) -
                                                             (!old.IsEmpty() && old.ContainsKey(s) && !old[s].IsEmpty() ? old[s]!.Value<int>() : 0);
                                                    break;
                                                case ArrayJoinMethod.Average:
                                                    res[s] = null;
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
                                        JObject old = oldMap[key];
                                        JObject res = resultMap[key];

                                        foreach (string s in valueFields)
                                        {
                                            switch (joinMethodMap.ContainsKey(s) ? joinMethodMap[s] : ArrayJoinMethod.Assign)
                                            {
                                                case ArrayJoinMethod.Assign:
                                                    break;
                                                case ArrayJoinMethod.Distinct:
                                                    if (!old.IsEmpty() && old.ContainsKey(s))
                                                        res[s] = old[s];
                                                    break;
                                                case ArrayJoinMethod.Sum:
                                                    res[s] = (res.ContainsKey(s) && !res[s].IsEmpty() ? res[s]!.Value<decimal>() : 0) -
                                                             (!old.IsEmpty() && old.ContainsKey(s) && !old[s].IsEmpty() ? old[s]!.Value<decimal>() : 0);
                                                    break;
                                                case ArrayJoinMethod.Count:
                                                    res[s] = (res.ContainsKey(s) && !res[s].IsEmpty() ? res[s]!.Value<int>() : 0) -
                                                             (!old.IsEmpty() && old.ContainsKey(s) && !old[s].IsEmpty() ? old[s]!.Value<int>() : 0);
                                                    break;
                                                case ArrayJoinMethod.Average:
                                                    res[s] = null;
                                                    break;
                                                default:
                                                    throw new ArgumentOutOfRangeException();
                                            }
                                        }
                                    }
                                }
                            }

                            // Convert the map to list, sorted by primary keys
                            JArray joinArray = new();
                            List<JObject> joinObjs = resultMap.Values.ToList();
                            joinObjs.Sort((a, b) =>
                            {
                                foreach (string s in array.Primary)
                                {
                                    switch (primaryNodes[s])
                                    {
                                        case ScalarNode { IsDate: true }:
                                            {
                                                DateTime ad = a[s]!.Value<DateTime>();
                                                DateTime bd = b[s]!.Value<DateTime>();
                                                if (!DateExtensions.Equal(ad, bd))
                                                    return DateExtensions.LessThan(ad, bd) ? -1 : 1;
                                                break;
                                            }
                                        case ScalarNode { IsNumber: true }:
                                            {
                                                decimal ad = a[s]!.Value<decimal>();
                                                decimal bd = b[s]!.Value<decimal>();
                                                if (ad != bd)
                                                    return ad < bd ? -1 : 1;
                                                break;
                                            }
                                        default:
                                            {
                                                string ad = a[s].ToString();
                                                string bd = b[s].ToString();
                                                if (!ad.Equals(bd))
                                                    return string.Compare(ad, bd, StringComparison.OrdinalIgnoreCase);
                                                break;
                                            }
                                    }
                                }
                                return 0;
                            });
                            foreach (JObject o in joinObjs)
                                joinArray.Add(o);

                            // Save to result
                            result = joinArray;
                            break;
                        }
                }

                // Save
                await SaveFieldDataAsync(tarField, realTarget, result, true, dropList: realTarget == target && (arrayIndex < 0 || args[arrayIndex].IsFull));
            }

            // Process next level
            root = root.Next;
        }

        // Process other targets
        foreach (string tar in otherTargets)
        {
            if (transChangedData.TryGetValue(tar, out TransactionChangeData val))
                await ProcessDataPush(tar, val);
        }
    }

    // Record the changed fields with changed values
    void OnFieldDataChanged(string target, AppFieldNode field, TransactionChangeOperation operation, JToken value = null, JToken origin = null)
    {
        TransactionChangeData changeData;
        if (transChangedData.ContainsKey(target))
        {
            changeData = transChangedData[target];
        }
        else
        {
            changeData = new TransactionChangeData();
            transChangedData.Add(target, changeData);
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

    // Drop data required to update the ref field
    bool IsDropDataRequired(AppFieldNode field) => field.Observers != null && field.Observers.Any(o => o.SourceNode != null || IsDropDataRequired(o));

    /// <summary>
    /// Get DbCommand.
    /// </summary>
    /// <returns></returns>
    DbCommand GetDbCommand()
    {
        DbCommand command = MySql.Context.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction != null ? transaction.GetDbTransaction() : MySql.Context.Database.CurrentTransaction?.GetDbTransaction();
        return command;
    }

    #endregion

    #region Group Join

    /// <summary>
    /// Join to scalar
    /// </summary>
    public async Task<JValue> GroupJoin(ScalarNode node, JToken value, ArrayJoinMethod method)
    {
        JValue result = value switch
        {
            // Direct
            JValue val => val,
            // Join
            JArray { Count: > 0 } newArray =>
                // Get by join methods
                method switch
                {
                    // Join the data
                    ArrayJoinMethod.Assign => newArray.LastOrDefault(p => p is JValue) as JValue,
                    ArrayJoinMethod.Count => new JValue(newArray.Count),
                    ArrayJoinMethod.Sum => node.IsNumber ? new JValue(newArray.Sum(token => token.Value<decimal>())) : null,
                    ArrayJoinMethod.Distinct => newArray.FirstOrDefault(p => p is JValue) as JValue,
                    ArrayJoinMethod.Average => node.IsNumber && newArray.Count > 0 ? new JValue(newArray.Average(token => token.Value<decimal>())) : null,
                    ArrayJoinMethod.Min => node.IsNumber && newArray.Count > 0 ? new JValue(newArray.Min(token => token.Value<decimal>())) : null,
                    ArrayJoinMethod.Max => node.IsNumber && newArray.Count > 0 ? new JValue(newArray.Max(token => token.Value<decimal>())) : null,
                    _ => throw new ArgumentOutOfRangeException(nameof(method), method, null)
                },
            _ => null
        };
        if (result.IsEmpty()) return null;
        (JToken res, JToken error) = await node.ValidateValue(this, result);
        return error.IsEmpty() ? res as JValue : throw new Exception(error.ToString());
    }

    /// <summary>
    /// Join to struct
    /// </summary>
    public async Task<JObject> GroupJoin(StructNode node, JToken value, IReadOnlyDictionary<string, ArrayJoinMethod> joinMethodMap)
    {
        if (value.IsEmpty() || node.Fields == null || node.Fields.Count == 0) return null;
        switch (value)
        {
            case JObject:
                {
                    (JToken res, JToken error) = await node.ValidateValue(this, value);
                    return error.IsEmpty() ? res as JObject : throw new Exception(error.ToString());
                }
            case JArray { Count: > 0 } array:
                {
                    // Valiate the result
                    JArray validateArray = new();
                    foreach (JToken token in array)
                    {
                        (JToken res, JToken error) = await node.ValidateValue(this, token);
                        validateArray.Add(error.IsEmpty() ? res : throw new Exception(error.ToString()));
                    }
                    array = validateArray;
                    if (array.IsEmpty()) return null;

                    // Join
                    JObject result = new();
                    foreach (StructNodeField field in node.Fields)
                    {
                        switch (joinMethodMap.ContainsKey(field.Name) ? joinMethodMap[field.Name] : ArrayJoinMethod.Assign)
                        {
                            case ArrayJoinMethod.Assign:
                                {
                                    JObject last = (JObject)array.LastOrDefault(p => p is JObject obj && obj.ContainsKey(field.Name));
                                    if (last != null)
                                        result[field.Name] = last[field.Name];
                                    break;
                                }
                            case ArrayJoinMethod.Distinct:
                                {
                                    JObject first = (JObject)array.FirstOrDefault(p => p is JObject obj && obj.ContainsKey(field.Name));
                                    if (first != null)
                                        result[field.Name] = first[field.Name];
                                    break;
                                }
                            case ArrayJoinMethod.Sum:
                                result[field.Name] = field.TypeNode is ScalarNode { IsNumber: true } ? array.Sum(p => p is JObject obj && obj.ContainsKey(field.Name) && obj[field.Name] is JValue val && !val.IsEmpty() ? val.Value<decimal>() : 0) : null;
                                break;
                            case ArrayJoinMethod.Count:
                                result[field.Name] = field.TypeNode is ScalarNode { IsNumber: true } ? array.Count : null;
                                break;
                            case ArrayJoinMethod.Average:
                                result[field.Name] = field.TypeNode is ScalarNode { IsNumber: true } ? array.Average(p => p is JObject obj && obj.ContainsKey(field.Name) && obj[field.Name] is JValue val && !val.IsEmpty() ? val.Value<decimal>() : 0) : null;
                                break;
                            case ArrayJoinMethod.Min:
                                {
                                    if (field.TypeNode is ScalarNode { IsNumber: true })
                                    {
                                        decimal[] valArray = array.Where(p => p is JObject obj && obj.ContainsKey(field.Name) && obj[field.Name] is JValue val && !val.IsEmpty()).Select(p => p[field.Name]!.Value<decimal>()).ToArray();
                                        result[field.Name] = valArray.Length > 0 ? valArray.Min() : null;
                                    }
                                    else
                                    {
                                        result[field.Name] = null;
                                    }
                                    break;
                                }
                            case ArrayJoinMethod.Max:
                                {
                                    if (field.TypeNode is ScalarNode { IsNumber: true })
                                    {
                                        decimal[] valArray = array.Where(p => p is JObject obj && obj.ContainsKey(field.Name) && obj[field.Name] is JValue val && !val.IsEmpty()).Select(p => p[field.Name]!.Value<decimal>()).ToArray();
                                        result[field.Name] = valArray.Length > 0 ? valArray.Max() : null;
                                    }
                                    else
                                    {
                                        result[field.Name] = null;
                                    }
                                    break;
                                }
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
    public async Task<Dictionary<string, JObject>> GroupJoinObjectMap(ArrayNode node, JToken value, Dictionary<string, ArrayJoinMethod> joinMethodMap)
    {
        if (value.IsEmpty()) return new Dictionary<string, JObject>();

        // Gets field type
        StructNode @struct = (StructNode)node.BaseNode;
        List<string> valueFields = (from fieldType in @struct.Fields where !node.Primary.Contains(fieldType.Name) select fieldType.Name).ToList();

        // The element struct type
        switch (value)
        {
            // Check by value
            case JObject o when !o.IsEmpty():
                {
                    // Validate the value
                    (JToken res, JToken error) = await @struct.ValidateValue(this, o);
                    if (!error.IsEmpty()) throw new Exception(error.ToString());
                    if (res.IsEmpty() || res is not JObject resObj) break;
                    o = resObj;

                    // Check the primary key
                    string key = node.GetPrimaryKey(o);
                    if (string.IsNullOrWhiteSpace(key))
                        return new Dictionary<string, JObject>();

                    // Return single element array
                    return new Dictionary<string, JObject>
                    {
                        { key, o }
                    };
                }
            case JArray array:
                {
                    // The return list with order
                    Dictionary<string, JObject> keyMap = new();
                    Dictionary<string, int> keyCount = new();
                    foreach (JToken token in array)
                    {
                        if (token is not JObject obj) continue;

                        // Validate the value
                        (JToken res, JToken error) = await @struct.ValidateValue(this, obj);
                        if (!error.IsEmpty()) throw new Exception(error.ToString());
                        if (res.IsEmpty() || res is not JObject o) continue;
                        obj = o;

                        // Gets the key
                        string key = node.GetPrimaryKey(obj);
                        if (string.IsNullOrWhiteSpace(key)) continue;
                        if (keyMap.ContainsKey(key))
                        {
                            // Join the data fields
                            JObject total = keyMap[key];
                            keyCount[key]++;
                            foreach (string s in valueFields)
                            {
                                switch (joinMethodMap.ContainsKey(s) ? joinMethodMap[s] : ArrayJoinMethod.Assign)
                                {
                                    // 赋值
                                    case ArrayJoinMethod.Assign:
                                        {
                                            if (obj.ContainsKey(s) && !obj[s].IsEmpty())
                                                total[s] = obj[s];
                                            break;
                                        }

                                    // 取一个
                                    case ArrayJoinMethod.Distinct:
                                        if (!(total.ContainsKey(s) && !total[s].IsEmpty()) && obj.ContainsKey(s) && !obj[s].IsEmpty())
                                            total[s] = obj[s];
                                        break;

                                    // 统计 X 平均
                                    case ArrayJoinMethod.Sum:
                                    case ArrayJoinMethod.Average:
                                        total[s] = (total.ContainsKey(s) && !total[s].IsEmpty() ? total[s]!.Value<decimal>() : 0) + (obj.ContainsKey(s) && !obj[s].IsEmpty() ? obj[s]!.Value<decimal>() : 0);
                                        break;

                                    // 计数
                                    case ArrayJoinMethod.Count:
                                        total[s] = (total.ContainsKey(s) && !total[s].IsEmpty() ? total[s]!.Value<int>() : 0) + 1;
                                        break;

                                    // 最小
                                    case ArrayJoinMethod.Min:
                                        if (obj.ContainsKey(s) && !obj[s].IsEmpty())
                                        {
                                            decimal val = obj[s].Value<decimal>();
                                            if (!total.ContainsKey(s) || total[s].IsEmpty() || val < total[s].Value<decimal>())
                                            {
                                                total[s] = val;
                                            }
                                        }
                                        break;

                                    // 最大
                                    case ArrayJoinMethod.Max:
                                        if (obj.ContainsKey(s) && !obj[s].IsEmpty())
                                        {
                                            decimal val = obj[s].Value<decimal>();
                                            if (!total.ContainsKey(s) || total[s].IsEmpty() || val > total[s].Value<decimal>())
                                            {
                                                total[s] = val;
                                            }
                                        }
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
                            foreach ((string s, ArrayJoinMethod m) in joinMethodMap)
                                if (m == ArrayJoinMethod.Count)
                                    obj[s] = 1;
                        }
                    }

                    // Calc average
                    foreach ((string s, ArrayJoinMethod m) in joinMethodMap)
                    {
                        if (m != ArrayJoinMethod.Average) continue;
                        foreach ((string key, JObject total) in keyMap)
                        {
                            if (total.ContainsKey(s))
                                total[s] = (!total[s].IsEmpty() ? total[s].Value<decimal>() : 0m) / keyCount[key];
                        }
                    }

                    // Gen the result
                    return keyMap;
                }
        }
        return new Dictionary<string, JObject>();
    }

    /// <summary>
    /// Join to array
    /// </summary>
    public async Task<JArray> GroupJoin(ArrayNode node, JToken value, Dictionary<string, ArrayJoinMethod> joinMethodMap)
    {
        if (node.BaseNode is not StructNode structNode) return null;
        Dictionary<string, NamespaceNode> primaryNodes = structNode.Fields.Where(fieldType => node.Primary.Contains(fieldType.Name)).ToDictionary(fieldType => fieldType.Name, fieldType => fieldType.TypeNode);

        // Result
        JArray joinArray = new();
        Dictionary<string, JObject> resultMap = await GroupJoinObjectMap(node, value, joinMethodMap);
        List<JObject> joinObjs = resultMap.Values.ToList();
        joinObjs.Sort((a, b) =>
        {
            foreach (string s in node.Primary)
            {
                switch (primaryNodes[s])
                {
                    case ScalarNode { IsDate: true }:
                        {
                            DateTime ad = a[s]!.Value<DateTime>();
                            DateTime bd = b[s]!.Value<DateTime>();
                            if (!ad.Equal(bd))
                                return ad.LessThan(bd) ? -1 : 1;
                            break;
                        }
                    case ScalarNode { IsNumber: true }:
                        {
                            decimal ad = a[s]!.Value<decimal>();
                            decimal bd = b[s]!.Value<decimal>();
                            if (ad != bd)
                                return ad < bd ? -1 : 1;
                            break;
                        }
                    default:
                        {
                            string ad = a[s].ToString();
                            string bd = b[s].ToString();
                            if (!ad.Equals(bd))
                                return string.Compare(ad, bd, StringComparison.OrdinalIgnoreCase);
                            break;
                        }
                }
            }
            return 0;
        });
        foreach (JObject o in joinObjs)
            joinArray.Add(o);
        return joinArray;
    }

    #endregion

    #region Validate Field Data Type

    /// <summary>
    /// Whether the function can be used for type validation(in: type, out: string)
    /// </summary>
    public async Task<bool> IsValidFuncForType(string type, string func, string @base = null)
    {
        NamespaceNode node = await GetNamespaceNodeAsync(func);
        return node is FunctionNode { ReturnNode: ScalarNode { IsString: true } } funcNode
               && ((funcNode.Args is { Count: 1 } && type.Equals(funcNode.Args[0].Type))
                   || (!string.IsNullOrWhiteSpace(@base) && funcNode.Args is { Count: 2 } && @base.Equals(funcNode.Args[0].Type) && (@base.Equals(funcNode.Args[1].Type) || funcNode.Args[1].UseArgType == 1)));
    }

    /// <summary>
    /// Validate the value with data type
    /// </summary>
    public async Task<(JToken value, JToken error)> ValidateValueByType(string typeName, JToken value)
    {
        // Valiadte value
        if (value.IsEmpty()) return (null, null);

        // validate the namespace
        NamespaceNode node = await GetNamespaceNodeAsync(typeName);
        if (node == null) return (value, TYPE_NAMESPACE_NOT_EXIST);
        return await node.ValidateValue(this, value);
    }

    /// <summary>
    /// Validate field by DynamicTableSchema and return nullable
    /// </summary>
    public async Task<(bool isEmpty, JToken result)> ValidateField(JToken token, DynamicTableSchema schema, [CallerArgumentExpression("token")] string paramName = null)
    {
        if (token.IsEmpty()) return (true, null);
        if (paramName!.IndexOf('.') > 0)
            paramName = paramName[(paramName.IndexOf('.') + 1)..];

        // Validate by the data type
        (JToken value, JToken errors) = await ValidateValueByType(schema.DataType, token);
        if (!errors.IsEmpty())
            throw CreateParameterException(paramName, errors);

        // check schema
        if (schema.Single)
        {
            if (schema.Fields.Count == 1 && schema.Fields[0].Name == DYNAMIC_TABLE_VALUE_FIELD)
            {
                if (schema.Fields[0].Type != DynamicTableFieldType.Json)
                {
                    // null, string, scalar
                    if (string.IsNullOrWhiteSpace(value.Value<string>()))
                    {
                        return (true, null);
                    }
                    CheckValue(value, schema.Fields[0].Type);
                }
                else if (value is JValue)
                {
                    throw CreateParameterException(paramName, IMPORT_DATA_FIELD_NOT_MATCH);
                }
            }
            else
            {
                if (value is not JObject jObject)
                    throw CreateParameterException(paramName, IMPORT_DATA_FIELD_NOT_MATCH);

                // empty obj
                if (jObject.Count == 0)
                {
                    return (true, value);
                }
                CheckObject(jObject, schema.Fields);
            }
        }
        else
        {
            if (value is not JArray arr)
                throw CreateParameterException(paramName, IMPORT_DATA_FIELD_NOT_MATCH);

            // empty arr
            if (arr.Count == 0)
            {
                return (true, value);
            }
            if (arr.Any(p => p.Type != JTokenType.Object))
                throw CreateParameterException(paramName, IMPORT_DATA_FIELD_NOT_MATCH);
            foreach (JToken subToken in arr)
            {
                CheckObject(subToken as JObject, schema.Fields);
            }
        }
        return (false, value);
    }

    /// <summary>
    /// Check the value type if match the field in database.
    /// </summary>
    static void CheckValue(JToken token, DynamicTableFieldType type)
    {
        if (token.IsEmpty()) return;
        try
        {
            switch (type)
            {
                case DynamicTableFieldType.Bool:
                    token.ToObject<bool>();
                    break;
                case DynamicTableFieldType.Smallint:
                    token.ToObject<short>();
                    break;
                case DynamicTableFieldType.USmallint:
                    token.ToObject<ushort>();
                    break;
                case DynamicTableFieldType.Int:
                case DynamicTableFieldType.Mediumint:
                    token.ToObject<int>();
                    break;
                case DynamicTableFieldType.UInt:
                case DynamicTableFieldType.UMediumint:
                    token.ToObject<uint>();
                    break;
                case DynamicTableFieldType.BigInt:
                    token.ToObject<long>();
                    break;
                case DynamicTableFieldType.UBigInt:
                    token.ToObject<ulong>();
                    break;
                case DynamicTableFieldType.Float:
                    token.ToObject<float>();
                    break;
                case DynamicTableFieldType.Double:
                    token.ToObject<double>();
                    break;
                case DynamicTableFieldType.DateTime:
                    token.ToObject<DateTime>();
                    break;
                case DynamicTableFieldType.Char:
                case DynamicTableFieldType.VarChar:
                case DynamicTableFieldType.TinyText:
                case DynamicTableFieldType.Text:
                case DynamicTableFieldType.MediumText:
                case DynamicTableFieldType.LongText:
                case DynamicTableFieldType.TinyBlob:
                case DynamicTableFieldType.Blob:
                case DynamicTableFieldType.MediumBlob:
                case DynamicTableFieldType.LongBlob:
                    if (token.Type != JTokenType.String) throw new Exception();
                    token.ToObject<string>();
                    break;
                case DynamicTableFieldType.Json:
                    break;
                default:
                    throw new Exception();
            }
        }
        catch (Exception)
        {
            throw CreateParameterException(new Dictionary<string, string>
            {
                { "Data", IMPORT_DATA_FIELD_TYPE_NOT_MATCH },
                { token.Parent != null ? token.Parent.ToJson() : token.ToJson(), IMPORT_DATA_FIELD_TYPE_NOT_MATCH }
            });
        }
    }

    /// <summary>
    /// Check the each item type of the JObject if match the field in database.
    /// </summary>
    static void CheckObject(JObject obj, List<DynamicTableField> fields)
    {
        foreach (DynamicTableField field in fields)
        {
            if (field.Complex == null)
            {
                if (!obj.ContainsKey(field.Name)) continue;
                if (field.Type != DynamicTableFieldType.Json)
                    CheckValue(obj[field.Name], field.Type);
            }
            else if (obj.ContainsKey(field.Complex.Main) && obj[field.Complex.Main] is JObject subObj && subObj.ContainsKey(field.Complex.Field))
            {
                if (field.Type != DynamicTableFieldType.Json)
                    CheckValue(subObj[field.Complex.Field], field.Type);
            }
        }
    }

    /// <summary>
    /// Creates an exception that represents a parameter error.
    /// </summary>
    [DebuggerHidden]
    static MicroserviceApiException CreateParameterException(string field, string message)
    {
        return new MicroserviceApiException(MicroserviceApiResponseErrorCode.InvalidParams, "The request parameters are invalid.", new Dictionary<string, object>
        {
            { field, message }
        });
    }

    /// <summary>
    /// Creates an exception that represents a parameter error.
    /// </summary>
    [DebuggerHidden]
    static MicroserviceApiException CreateParameterException(IDictionary<string, string> errorMessages)
    {
        IDictionary<string, object> errorData = errorMessages.ToDictionary(keyValuePair => keyValuePair.Key, keyValuePair => (object)keyValuePair.Value);
        return new MicroserviceApiException(MicroserviceApiResponseErrorCode.InvalidParams, "The request parameters are invalid.", errorData);
    }

    [DebuggerHidden]
    static MicroserviceApiException CreateParameterException(string field, JToken errors)
    {
        return new MicroserviceApiException(MicroserviceApiResponseErrorCode.InvalidParams, "The request parameters are invalid.", new Dictionary<string, object>
        {
            { field, errors }
        });
    }

    #endregion

    #region Validation

    /// <summary>
    /// Validate the parent namespace.
    /// </summary>
    public async Task<NamespaceNode> ValidateParentNamespaceAsync(string name, [CallerArgumentExpression("name")] string paramName = null)
    {
        string[] names = Regex.Split(name, @"\W+");
        if (names.Length <= 1) return null;
        name = string.Join(".", names.SkipLast(1));

        NamespaceNode entity = await GetNamespaceNodeAsync(name);
        if (entity is not { Type: NamespaceType.Namspace })
            throw CreateParameterException(paramName![(paramName.IndexOf('.') + 1)..], string.Format(TYPE_NAMESPACE_PARENT_NOT_VALID, name));

        return entity;
    }

    /// <summary>
    /// Validate the parent category.
    /// </summary>
    public async Task<AppNode> ValidateParentCategoryAsync(string name, [CallerArgumentExpression("name")] string paramName = null)
    {
        string[] names = Regex.Split(name, @"\W+");
        if (names.Length <= 1) return null;
        name = string.Join(".", names.SkipLast(1));

        AppNode entity = await GetAppNodeAsync(name);
        if (entity == null)
            throw CreateParameterException(paramName![(paramName.IndexOf('.') + 1)..], string.Format(CATEGORY_PARENT_NOT_VALID, name));

        return entity;
    }


    #endregion

    #endregion

    #region Utility

    // Call async function
    static T? CallAsyncFunc<T>(MethodBase asyncCall, params object[] callArgs)
    {
        Task<T>? task = (Task<T>?)asyncCall.Invoke(null, callArgs);
        return task == null ? default : task.GetAwaiter().GetResult();
    }

    // Gets the call async method
    static MethodInfo GetCallAsyncFunc(Type t) => CallAsyncMethodMap.GetOrAdd(t, p => typeof(SchemaContext).GetMethod(nameof(CallAsyncFunc), BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(p));


    private readonly Lazy<ILogger> _loggerThunk;
    
    static readonly ConcurrentDictionary<Type, MethodInfo> CallAsyncMethodMap = new();
    static readonly NamespaceNode RootNamespace;
    static readonly AppNode RootAppNode;

    #endregion
}