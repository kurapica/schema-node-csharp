using System.Text.RegularExpressions;
using SchemaNode.DI;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Provider;
using SchemaNode.Schema;

namespace SchemaNode.Context;

/// <summary>
/// The schema context
/// </summary>
public class SchemaContext
{
    #region Constructor

    static SchemaContext()
    {
        rootNamespace = new NamespaceNode
        {
            Name = "",
        };
    }
    
    public SchemaContext(IServiceProvider serviceProvider, ISchemaProvider schemaProvider)
    {
        SchemaProvider = schemaProvider;
        ServiceProvider = serviceProvider;
    }
    
    #endregion
    
    #region Static Properties

    public static SchemaContextConfig Config { get; set; } = new();

    #endregion
    
    #region Properties
    
    /// <summary>
    /// The service provider
    /// </summary>
    public IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// The schema provider
    /// </summary>
    public ISchemaProvider SchemaProvider { get; }

    #endregion
    
    #region Schema Methods

    /// <summary>
    /// Gets the schema node
    /// </summary>
    public async Task<NamespaceNode?> GetSchemaNodeAsync(string schemaName, bool reload = false, bool preload = false)
    {
        NamespaceNode? node = rootNamespace;
        if (string.IsNullOrWhiteSpace(schemaName)) return node;
        schemaName = schemaName.ToLowerInvariant();
        
        // gets the node
        string fullPath = "";
        foreach (string path in Regex.Split(schemaName, @"\W+"))
        {
            if (node is null) return null;
            NamespaceNode parent = node;
            if (parent.Type != SchemaType.Namespace) return null;
            fullPath = !string.IsNullOrWhiteSpace(fullPath) ? $"{fullPath}.{path}" : path;
            
            // Gets the sub node
            if (parent.Schemas != null && parent.Schemas.TryGetValue(path, out node))
                continue;
            
            // If prel
            if (Config.PreLoad && !preload) return null;

            NodeSchema? schema = await SchemaProvider.LoadSchema(fullPath);
            if (schema is null) return null;
        }
    }
    
    #endregion
    
    #region Static Utility
    
    static readonly NamespaceNode rootNamespace;
    
    #endregion
}