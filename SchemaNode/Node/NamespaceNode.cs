using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Components.Provider;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Node;

/// <summary>
/// The in-memory schema representation
/// </summary>
public class NamespaceNode: IDisposable
{
    #region Data

    /// <summary>
    /// The namespace
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The schema display
    /// </summary>
    public LocaleString? Display { get; set; }

    #endregion
    
    #region Status
    
    /// <summary>
    /// The schema type
    /// </summary>
    public virtual SchemaType Type => SchemaType.Namespace;

    /// <summary>
    /// Is value type
    /// </summary>
    public virtual bool IsValueType => Type != SchemaType.Namespace && Type != SchemaType.Function;

    /// <summary>
    /// The load state
    /// </summary>
    public SchemaLoadState LoadState { get; init; } = SchemaLoadState.Server;

    /// <summary>
    /// The schema node status
    /// </summary>
    public SchemaNodeStatus Status { get; set; } = SchemaNodeStatus.Ready;
    
    /// <summary>
    /// The scheme provider used to load the node
    /// </summary>
    public ISchemaProvider? SchemaProvider { get; set; } = null;
    
    /// <summary>
    /// The Sub namespaces
    /// </summary>
    public ConcurrentDictionary<string, NamespaceNode>? Schemas { get; set; }
    
    /// <summary>
    /// Whether the node is used
    /// </summary>
    public bool IsUsed => UsedBy is { IsEmpty: false };

    #endregion
    
    #region Methods

    /// <summary>
    /// Load the schema data
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="schema">The schema</param>
    /// <param name="preload">Whether during preload</param>
    public virtual async Task LoadAsync(SchemaContext context, NodeSchema schema, bool preload = false)
    {
        Schemas ??= new ConcurrentDictionary<string, NamespaceNode>();

        if (SchemaContext.Config.PreLoad && preload)
        {
            if (schema.Schemas == null || schema.Schemas.Length == 0) return;

            // scalar
            foreach (NodeSchema s in schema.Schemas.Where(s => s.Type == SchemaType.Scalar))
                await context.GetSchemaNodeAsync(s.Name, preload: true);
            
            // enum
            foreach (NodeSchema s in schema.Schemas.Where(s => s.Type == SchemaType.Enum))
                await context.GetSchemaNodeAsync(s.Name, preload: true);
            
            // struct
            foreach (NodeSchema s in schema.Schemas.Where(s => s.Type == SchemaType.Struct))
                await context.GetSchemaNodeAsync(s.Name, preload: true);
            
            // array
            foreach (NodeSchema s in schema.Schemas.Where(s => s.Type == SchemaType.Array))
                await context.GetSchemaNodeAsync(s.Name, preload: true);
            
            // function
            foreach (NodeSchema s in schema.Schemas.Where(s => s.Type == SchemaType.Function))
                await context.GetSchemaNodeAsync(s.Name, preload: true);
                    
            // namespace
            foreach (NodeSchema s in schema.Schemas.Where(s => s.Type == SchemaType.Namespace))
                await context.GetSchemaNodeAsync(s.Name, preload: true);
        }
    }

    /// <summary>
    /// Release the refs
    /// </summary>
    public virtual void Release()
    {
    }

    /// <summary>
    /// Used by another node
    /// </summary>
    public void AddRef(NamespaceNode node)
    {
        UsedBy ??= new ConcurrentDictionary<NamespaceNode, bool>();
        UsedBy.TryAdd(node, true);
    }

    /// <summary>
    /// Remove a ref from another node
    /// </summary>
    public void RemoveRef(NamespaceNode node)
    {
        UsedBy?.TryRemove(node, out _);
    }

    /// <summary>
    /// Validate the value with the schema
    /// </summary>
    public virtual async Task<(JsonNode? value, JsonNode? error)> ValidateValueAsync(SchemaContext context, JsonNode value)
    {
        await Task.Yield();
        return (value, TYPE_NAMESPACE_NOT_DATA_TYPE);
    }

    /// <summary>
    /// Whether the schema type can be used as the other
    /// </summary>
    public virtual bool CanBeUseAs(NamespaceNode other) => Name.Equals(other.Name);
    
    /// <summary>
    /// Gets the array node that use this node as element
    /// </summary>
    public virtual ArrayNode? GetArrayNode(bool exactly = false) =>
        UsedBy?.Keys.FirstOrDefault(p => p is ArrayNode array && array.ElementNode == this) as ArrayNode
        ?? (!exactly ? UsedBy?.Keys.FirstOrDefault(p => p is ArrayNode array && array.ElementNode != null && CanBeUseAs(array.ElementNode)) as ArrayNode : null); 
    
    /// <summary>
    /// Whether the type can be used as data index
    /// </summary>
    public virtual bool IsIndexable => false;

    public void Dispose()
    {
        Release();
    }

    #endregion
    
    #region Conversion

    /// <summary>
    /// Convert the schema to node
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static implicit operator NamespaceNode?(NodeSchema? schema)
    {
        if (schema == null) return null;
        return schema.Type switch
        {
            SchemaType.Namespace => new NamespaceNode { Name = schema.Name, Display = schema.Display, LoadState = schema.LoadState ?? SchemaLoadState.Server, SchemaProvider = schema.SchemaProvider },
            SchemaType.Scalar => new ScalarNode { Name = schema.Name, Display = schema.Display, LoadState = schema.LoadState ?? SchemaLoadState.Server, SchemaProvider = schema.SchemaProvider },
            SchemaType.Enum => new EnumNode { Name = schema.Name, Display = schema.Display, LoadState = schema.LoadState ?? SchemaLoadState.Server, SchemaProvider = schema.SchemaProvider },
            SchemaType.Struct => new StructNode { Name = schema.Name, Display = schema.Display, LoadState = schema.LoadState ?? SchemaLoadState.Server, SchemaProvider = schema.SchemaProvider },
            SchemaType.Array => new ArrayNode { Name = schema.Name, Display = schema.Display, LoadState = schema.LoadState ?? SchemaLoadState.Server, SchemaProvider = schema.SchemaProvider },
            SchemaType.Function => new FunctionNode { Name = schema.Name, Display = schema.Display, LoadState = schema.LoadState ?? SchemaLoadState.Server, SchemaProvider = schema.SchemaProvider },
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    /// <summary>
    /// Convert the node to schema
    /// </summary>
    public static implicit operator NodeSchema?(NamespaceNode? schema)
    {
        if (schema == null) return null;
        return new NodeSchema
        {
            Name = schema.Name,
            Type = schema.Type,
            Display = schema.Display,
            LoadState = schema.LoadState,
        };
    }
    
    #endregion

    #region Utility
    
    /// <summary>
    /// Used by other types
    /// </summary>
    protected ConcurrentDictionary<NamespaceNode, bool>? UsedBy;

    #endregion
}