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

namespace SchemaNode.Context;

/// <summary>
/// The schema context
/// </summary>
public class SchemaContext
{
    #region Constructor

    static SchemaContext()
    {
        RootNamespace = new NamespaceNode
        {
            Name = "",
        };
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
            if (args.Count < i || args[i] == null)
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
                result = ((dynamic)funcInfo.DynamicMethod!).DynamicInvoke(callArgs);
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
        bool res = await provider.SaveSchemaAsync(schema);
        if (res)
        {
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
        }
        return res;
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
        bool res = await provider.DeleteSchemaAsync(name);
        if (res)
        {
            RemoveSchemaNode(name);
            await this.PublishMessageAsync(new SchemaChangeMessage
            {
                DeleteSchemas = [name]
            });
        }
        return res;
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
    
    #endregion
}