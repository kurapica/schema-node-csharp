using SchemaNode.Context;
using SchemaNode.Schema;
using System.Collections.Concurrent;
using SchemaNode.Property;
using SchemaNode.Property.Record;

namespace SchemaNode.Runtime;

/// <summary>
/// The namespace node
/// </summary>
public sealed class NamespaceType: NodeType
{
    #region Field

    private readonly ConcurrentDictionary<string, NodeSchema> _schemas = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, NodeType> _types = new(StringComparer.OrdinalIgnoreCase);
    private ConcurrentDictionary<string, NodeSchema>.AlternateLookup<ReadOnlySpan<char>>? _schemaLookup;
    private ConcurrentDictionary<string, NodeType>.AlternateLookup<ReadOnlySpan<char>>? _typeLookup;

    #endregion

    #region Method

    /// <summary>
    /// Gets the node schema by name
    /// </summary>
    internal NodeSchema? GetNodeSchema(ReadOnlySpan<char> name)
    {
        _schemaLookup ??= _schemas.GetAlternateLookup<ReadOnlySpan<char>>();
        return _schemaLookup.Value.TryGetValue(name, out NodeSchema? schema) ? schema : null;
    }

    /// <summary>
    /// Get all node schemas
    /// </summary>
    internal IEnumerable<NodeSchema> GetNodeSchemas() => _schemas.Values;

    /// <summary>
    /// Gets the node schema
    /// </summary>
    public NodeSchema? GetNodeSchema(SchemaContext context, ReadOnlySpan<char> name) => GetNodeSchema(name)?.Clone(context.Runtime);

    /// <summary>
    /// Get all node schemas
    /// </summary>
    public IEnumerable<NodeSchema> GetNodeSchemas(SchemaContext context)
    {
        // get order
        Dictionary<string, int> order = new (StringComparer.OrdinalIgnoreCase);
        foreach (IOrderProperty recordedValue in typeof(NodeSchemaKind).GetRecordedValues())
            order[recordedValue.GetValue<string>()!] = recordedValue.Order;
        
        // return the clone schemas
        return _schemas.Values.OrderBy(s => order.GetValueOrDefault(s.Kind, 0))
            .Select(s => s.Clone(context.Runtime));
    }

    /// <summary>
    /// Gets the saved node type
    /// </summary>
    public NodeType? GetNodeType(ReadOnlySpan<char> name)
    {
        _typeLookup ??= _types.GetAlternateLookup<ReadOnlySpan<char>>();
        return _typeLookup.Value.TryGetValue(name, out NodeType? type) ? type : null;
    }
    
    /// <summary>
    /// Saves the node type by segment name (not full name, since Schema may not be set yet)
    /// </summary>
    internal void SaveNodeType(ReadOnlySpan<char> name, NodeType nodeType) => _types[name.ToString()] = nodeType;

    /// <summary>
    /// Save the node schema to the namespace (keyed by partial Name, consistent with LoadAsync)
    /// </summary>
    internal void SaveNodeSchema(NodeSchema schema)
    {
        _schemas[schema.Name] = schema;
        // reload the type with new schema
        if (_types.TryGetValue(schema.Name, out NodeType? type)) type.Loaded = false;
    } 
    
    /// <summary>
    /// Remove node schema
    /// </summary>
    internal void RemoveNodeSchema(ReadOnlySpan<char> name)
    {
        string typeName = name.ToString();
        _schemas.TryRemove(typeName, out _);
        _types.TryRemove(typeName, out _);
    }

    /// <summary>
    /// Rest loading stage for reload
    /// </summary>
    internal void ResetLoadState()
    {
        Loaded = false;
        foreach (var nodeType in _types.Values)
        {
            if (nodeType is NamespaceType namespaceType)
                namespaceType.ResetLoadState();
            else
                nodeType.Loaded = false;
        }
    }

    /// <summary>
    /// Whether the node is used
    /// </summary>
    public override bool IsUsed => _schemas.Count > 0;

    #endregion
}
