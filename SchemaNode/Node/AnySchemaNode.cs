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
public abstract class AnySchemaNode: IDisposable
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
    /// Whether the node is used
    /// </summary>
    public virtual bool IsUsed => UsedBy is { IsEmpty: false };

    #endregion

    #region Methods

    /// <summary>
    /// Load the schema data
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="schema">The schema</param>
    /// <param name="preload">Whether during preload</param>
    public virtual Task LoadAsync(SchemaContext context, NodeSchema schema, bool preload = false) { return Task.CompletedTask; }
    
    /// <summary>
    /// Release the refs
    /// </summary>
    public virtual void Release()
    {
    }

    /// <summary>
    /// Used by another node
    /// </summary>
    public void AddRef(AnySchemaNode node)
    {
        UsedBy ??= new ConcurrentDictionary<AnySchemaNode, bool>();
        UsedBy.TryAdd(node, true);
    }

    /// <summary>
    /// Remove a ref from another node
    /// </summary>
    public void RemoveRef(AnySchemaNode node)
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
    public virtual bool CanBeUseAs(AnySchemaNode other) => Name.Equals(other.Name);
    
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
    public static implicit operator AnySchemaNode?(NodeSchema? schema)
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
    public static implicit operator NodeSchema?(AnySchemaNode? schema)
    {
        if (schema == null) return null;
        return schema.Type switch
        {
            SchemaType.Scalar => (NodeSchema)(schema as ScalarNode)!,
            SchemaType.Enum => (NodeSchema)(schema as EnumNode)!,
            SchemaType.Struct => (NodeSchema)(schema as StructNode)!,
            SchemaType.Array => (NodeSchema)(schema as ArrayNode)!,
            SchemaType.Function => (NodeSchema)(schema as FunctionNode)!,
            _ => (NodeSchema)(schema as NamespaceNode)!
        };
    }
    
    #endregion

    #region Utility
    
    /// <summary>
    /// Used by other types
    /// </summary>
    protected ConcurrentDictionary<AnySchemaNode, bool>? UsedBy;

    #endregion
}