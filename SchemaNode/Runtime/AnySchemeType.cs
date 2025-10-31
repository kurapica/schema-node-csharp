using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;
using SchemaNode.Node;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory schema representation
/// </summary>
public abstract class AnySchemeType: IDisposable
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
    public virtual bool IsValueType => Type != SchemaType.Namespace && Type != SchemaType.Func;

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
    public Type? SchemaProvider { get; set; }
    
    /// <summary>
    /// Whether the node is used
    /// </summary>
    public virtual bool IsUsed => UsedBy is { IsEmpty: false } || UsedByApp is { IsEmpty: false };
    
    /// <summary>
    /// The type is loaded
    /// </summary>
    internal bool Loaded { get; set; }

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
    public virtual void Release() { }

    /// <summary>
    /// Used by another node
    /// </summary>
    public void AddRef(AnySchemeType node)
    {
        UsedBy ??= new ConcurrentDictionary<AnySchemeType, bool>();
        UsedBy.TryAdd(node, true);
    }

    /// <summary>
    /// Used by an application field
    /// </summary>
    public void AddRef(AppFieldType type)
    {
        UsedByApp ??= new ConcurrentDictionary<AppFieldType, bool>();
        UsedByApp.TryAdd(type, true);
    }

    /// <summary>
    /// Remove a ref from another node
    /// </summary>
    public void RemoveRef(AnySchemeType node)
    {
        UsedBy?.TryRemove(node, out _);
    }

    /// <summary>
    /// Remove ref for an application field
    /// </summary>
    /// <param name="type"></param>
    public void RemoveRef(AppFieldType type)
    {
        UsedByApp?.TryRemove(type, out _);
    }

    public virtual AnySchemaNode? CreateNode(object? value = null) => Type switch
    {
        SchemaType.Scalar => new ScalarTypeNode((ScalarType)this, value),
        SchemaType.Enum => new EnumTypeNode((EnumType)this, value),
        SchemaType.Struct => new StructTypeNode((StructType)this, value),
        SchemaType.Array => new ArrayTypeNode((ArrayType)this, value),
        SchemaType.Json => new JsonTypeNode((JsonType)this, value),
        _ => null
    };

    /// <summary>
    /// Validate the value with the schema
    /// </summary>
    public virtual async Task<(AnySchemaNode? value, JsonNode? error)> ValidateValueAsync(SchemaContext context, JsonNode value)
    {
        await Task.Yield();
        return (null, TYPE_NAMESPACE_NOT_DATA_TYPE);
    }

    /// <summary>
    /// Whether the schema type can be used as the other
    /// </summary>
    public virtual bool CanBeUseAs(AnySchemeType other) => Name.Equals(other.Name);
    
    /// <summary>
    /// Gets the array node that use this node as element
    /// </summary>
    public virtual ArrayType? GetArrayNode(bool exactly = false) =>
        UsedBy?.Keys.FirstOrDefault(p => p is ArrayType array && array.ElementSchemaType == this) as ArrayType
        ?? (!exactly ? UsedBy?.Keys.FirstOrDefault(p => p is ArrayType array && array.ElementSchemaType != null && CanBeUseAs(array.ElementSchemaType)) as ArrayType : null); 
    
    /// <summary>
    /// Whether the type can be used as data index
    /// </summary>
    public virtual bool IsIndexable => false;
    
    /// <summary>
    /// Whether the new schema is valid for updating
    /// </summary>
    public virtual bool IsUpdatable(AnySchemeType other) => Type == other.Type;

    /// <summary>
    /// Release ref
    /// </summary>
    public void Dispose() => Release();

    /// <summary>
    /// Gets the depends schema nodes
    /// </summary>
    /// <returns></returns>
    public virtual IEnumerable<AnySchemeType> GetDependNodes()
    {
        yield break;
    }

    /// <summary>
    /// Gets all node schemas used by the node schema
    /// </summary>
    /// <returns></returns>
    public async Task<NodeSchema[]> GetNodeSchemas(SchemaContext ctx, NodeSchema? root = null, HashSet<string>? types = null, bool includeUsedBy = false, CancellationToken? cancellationToken = null)
    {
        types ??= new HashSet<string>();
        root ??= new NodeSchema
        {
            Name = "",
            Type = SchemaType.Namespace,
            Schemas = []
        };
        if (!types.Add(Name)) return root.Schemas!;
        
        // install
        string[] paths = Name.Split('.', StringSplitOptions.RemoveEmptyEntries);
        string fullPath = string.Empty;
        NodeSchema parent = root;
        for (int i = 0; i < paths.Length - 1; i++)
        {
            string p = paths[i];
            fullPath = string.IsNullOrWhiteSpace(fullPath) ? p : $"{fullPath}.{p}";
                
            parent.Schemas ??= [];
            NodeSchema? sub = parent.Schemas.FirstOrDefault(s => s.Name.Equals(fullPath, StringComparison.OrdinalIgnoreCase));
            if (sub == null)
            {
                cancellationToken?.ThrowIfCancellationRequested();
                
                AnySchemeType type = await ctx.GetSchemaTypeAsync(fullPath) ?? new TypeNamespace { Name = fullPath };
                sub = type;
                parent.Schemas = parent.Schemas == null ? [sub!] : parent.Schemas.Append(sub!).ToArray();
            }
            parent = sub!;
        }

        NodeSchema schema = this!;
        if (includeUsedBy)
        {
            schema.UsedBy = UsedBy?.Keys.Select(p => p.Name).ToArray();
            schema.UsedByApp = UsedByApp?.Keys.Select(p => p.App).Distinct().ToArray();
        }
        
        parent.Schemas ??= [];
        parent.Schemas = parent.Schemas.Append(schema).ToArray();

        // add dependencies
        foreach (AnySchemeType n in GetDependNodes())
        {
            cancellationToken?.ThrowIfCancellationRequested();
            await n.GetNodeSchemas(ctx, root, types);
        }

        return root.Schemas!;
    }

    #endregion
    
    #region Conversion

    /// <summary>
    /// Convert the schema to node
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static implicit operator AnySchemeType?(NodeSchema? schema)
    {
        if (schema == null) return null;
        return schema.Type switch
        {
            SchemaType.Namespace => new TypeNamespace { Name = schema.Name, Display = schema.Display, LoadState = schema.LoadState ?? SchemaLoadState.Server, SchemaProvider = schema.SchemaProvider },
            SchemaType.Scalar => new ScalarType { Name = schema.Name, Display = schema.Display, LoadState = schema.LoadState ?? SchemaLoadState.Server, SchemaProvider = schema.SchemaProvider },
            SchemaType.Enum => new EnumType { Name = schema.Name, Display = schema.Display, LoadState = schema.LoadState ?? SchemaLoadState.Server, SchemaProvider = schema.SchemaProvider },
            SchemaType.Struct => new StructType { Name = schema.Name, Display = schema.Display, LoadState = schema.LoadState ?? SchemaLoadState.Server, SchemaProvider = schema.SchemaProvider },
            SchemaType.Array => new ArrayType { Name = schema.Name, Display = schema.Display, LoadState = schema.LoadState ?? SchemaLoadState.Server, SchemaProvider = schema.SchemaProvider },
            SchemaType.Func => new FunctionType { Name = schema.Name, Display = schema.Display, LoadState = schema.LoadState ?? SchemaLoadState.Server, SchemaProvider = schema.SchemaProvider },
            SchemaType.Json => new JsonType{ Name = schema.Name, Display = schema.Display, LoadState = schema.LoadState ?? SchemaLoadState.Server },
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    /// <summary>
    /// Convert the node to schema
    /// </summary>
    public static implicit operator NodeSchema?(AnySchemeType? schema)
    {
        if (schema == null) return null;
        return schema.Type switch
        {
            SchemaType.Scalar => (schema as ScalarType),
            SchemaType.Enum => (schema as EnumType),
            SchemaType.Struct => (schema as StructType),
            SchemaType.Array => (schema as ArrayType),
            SchemaType.Func => (schema as FunctionType),
            SchemaType.Json => (schema as JsonType),
            _ => (schema as TypeNamespace)
        };
    }
    
    #endregion

    #region Utility
    
    internal ConcurrentDictionary<AnySchemeType, bool>? UsedBy;
    internal ConcurrentDictionary<AppFieldType, bool>? UsedByApp;

    #endregion
}