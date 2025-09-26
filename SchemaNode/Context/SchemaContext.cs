using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchemaNode.Components;
using SchemaNode.Components.Provider;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Schema;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static SchemaNode.Utility.Schema;

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
        if (node.SchemaProvider != null)
        {
            return await node.SchemaProvider.CallFunctionAsync(node.Name, args, generic);
        }
        else
        {
            foreach (ISchemaProvider provider in ServiceProvider.GetServices<ISchemaProvider>())
            {
                try
                {
                    JsonNode? result = await provider.CallFunctionAsync(node.Name, args, generic);
                    node.SchemaProvider = provider;
                    return result;
                }
                catch
                {
                    //pass
                }
            }
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
        if (node is FunctionNode funcNode) return await CallFunctionAsync(funcNode, args, generic);
        return null;
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
        if (string.IsNullOrWhiteSpace(schemaName) && !preload) return node;
        
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
        if (!reload && !preload) return node;
        
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


    private readonly Lazy<ILogger> _loggerThunk;
    
    static readonly AnySchemaNode RootNamespace;
    
    #endregion
}