using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using SchemaNode.DI;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Provider;
using SchemaNode.Schema;
using Microsoft.Extensions.Logging;
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
    
    public SchemaContext(IServiceProvider serviceProvider, ISchemaProvider schemaProvider)
    {
        SchemaProvider = schemaProvider;
        _loggerThunk = new Lazy<ILogger>(serviceProvider.GetRequiredService<ILogger<SchemaContext>>);
    }
    
    #endregion
    
    #region Static Properties

    public static SchemaContextConfig Config { get; set; } = new();

    #endregion
    
    #region Properties
    
    /// <summary>
    /// The schema provider
    /// </summary>
    public ISchemaProvider SchemaProvider { get; }
    
    /// <summary>
    /// Gets the logger
    /// </summary>
    public ILogger Logger => _loggerThunk.Value;

    #endregion
    
    #region Schema Methods

    /// <summary>
    /// Gets the schema node
    /// </summary>
    public async Task<NamespaceNode?> GetSchemaNodeAsync(string schemaName, bool reload = false, bool preload = false)
    {
        NamespaceNode? node = RootNamespace;
        if (string.IsNullOrWhiteSpace(schemaName)) return node;
        
        // gets the node
        string fullPath = "";
        foreach (string path in Regex.Split(schemaName.Trim().ToLowerInvariant(), @"\W+"))
        {
            NamespaceNode parent = node;
            if (parent.Type != SchemaType.Namespace) return null;
            fullPath = !string.IsNullOrWhiteSpace(fullPath) ? $"{fullPath}.{path}" : path;
            
            // Gets the sub node
            if (parent.Schemas != null && parent.Schemas.TryGetValue(path, out node))
                continue;
            
            // All should be preloaded
            if (Config.PreLoad && !preload) return null;

            // system schema first
            NodeSchema? schema = await GetNodeSchemaAsync(fullPath);
            node = schema;
            if (node is null) return null;
            
            parent.Schemas ??= new ConcurrentDictionary<string, NamespaceNode>();
            if (parent.Schemas.TryAdd(path, node))
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
                node = parent.Schemas[path];
                reload = false;
            }
        }
        if (!reload) return node;
        
        // reload the node
        NodeSchema? newSchema = await GetNodeSchemaAsync(fullPath);
        if (newSchema != null)
        {
            node.Display = newSchema.Display;
            node.Release();
            node.Status = SchemaNodeStatus.Ready;
            await node.LoadAsync(this, newSchema!, preload);
        }
        return node;
    }
    
    #endregion
    
    #region Utility

    async Task<NodeSchema?> GetNodeSchemaAsync(string fullPath)
    {
        NodeSchema? schema = GetSystemNodeSchema(fullPath);
        if (schema == null) return await SchemaProvider.LoadSchemaAsync(fullPath);

        if (schema.Type == SchemaType.Namespace)
        {
            // make sure don't change schema from system
            NodeSchema? server = await SchemaProvider.LoadSchemaAsync(fullPath);
            if (server?.Schemas == null) return schema;
            if (schema.Schemas is { Length: > 0 })
            {
                server.Schemas = server.Schemas.Concat(schema.Schemas.Where(s => !server.Schemas.Any(v => s.Name.Equals(v.Name, StringComparison.OrdinalIgnoreCase))).ToArray()).ToArray();
            }
            schema = server;
        }
        return schema;
    }

    private readonly Lazy<ILogger> _loggerThunk;
    
    static readonly NamespaceNode RootNamespace;
    
    #endregion
}