using SchemaNode.Context;
using SchemaNode.Schema;
using System.Collections.Concurrent;
using Microsoft.CSharp.RuntimeBinder;
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

    #endregion

    #region Method

    /// <inheritdoc/>
    public override Task LoadAsync(SchemaContext context, NodeSchema schema)
    {
        _schemas.Clear();
        if (schema.Schemas == null || schema.Schemas.Length == 0) return Task.CompletedTask;
        
        // record
        foreach (NodeSchema nodeSchema in schema.Schemas)
            _schemas[nodeSchema.Name] = nodeSchema;
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the node schema by name
    /// </summary>
    internal NodeSchema? GetNodeSchema(string name) => _schemas.GetValueOrDefault(name);

    /// <summary>
    /// Gets the node schema
    /// </summary>
    public NodeSchema? GetNodeSchema(SchemaContext context, string name)
    {
        NodeSchema? schema = _schemas.GetValueOrDefault(name);
        return schema?.Clone(context.Runtime.GetSchemaKindProperties(schema.Kind));
    }

    /// <summary>
    /// Get all node schemas
    /// </summary>
    internal IEnumerable<NodeSchema> GetNodeSchemas() => _schemas.Values;

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
            .Select(s => s.Clone(context.Runtime.GetSchemaKindProperties(s.Kind)));
    }
    
    /// <summary>
    /// Gets the saved node type
    /// </summary>
    public NodeType? GetNodeType(string name) => _types.GetValueOrDefault(name);
    
    /// <summary>
    /// Saves the node type
    /// </summary>
    internal void SaveNodeType(NodeType nodeType) => _types[nodeType.Name] = nodeType;
    
    /// <summary>
    /// Save the node schema to the namespace
    /// </summary>
    internal void SaveNodeSchema(NodeSchema schema) => _schemas[schema.FullName] = schema;
    
    /// <summary>
    /// Remove node schema
    /// </summary>
    internal void RemoveNodeSchema(string name)
    {
        _schemas.TryRemove(name, out _);
        _types.TryRemove(name, out _);
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
