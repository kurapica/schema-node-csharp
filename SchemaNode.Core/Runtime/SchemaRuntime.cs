using System.Collections.Concurrent;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Schema;
using SchemaNode.Struct;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

/// <summary>
/// The schema run-time with all run-time schema information, such as the schema types, properties, and so on.
/// It will be built by the stage handlers in the build stage and used in the runtime stage.
/// Normally it'd be a singleton instance for one service.
/// </summary>
public class SchemaRuntime : ISchemaRuntime
{
    #region Implementation of ISchemaRuntime

    private (string kind, Type schemaType, Type[]? properties)[] _schemaKinds = [];
    private readonly object _schemaKindsLock = new();

    /// <inheritdoc/>
    public void RegisterSchemaKind(string kind, Type schemaType, Type[]? properties = null)
    {
        lock (_schemaKindsLock)
            _schemaKinds = _schemaKinds.Append((kind, schemaType, properties)).ToArray();
    }

    /// <inheritdoc/>
    public IEnumerable<(string kind, Type schemaType)> GetSchemaKinds()
        => _schemaKinds.Select(k => (k.kind, k.schemaType));

    /// <inheritdoc/>
    public IEnumerable<Type> GetSchemaKindProperties(string kind)
        => _schemaKinds.FirstOrDefault(k => k.kind.Equals(kind, StringComparison.OrdinalIgnoreCase)).properties ?? [];

    /// <inheritdoc/>
    public Type? GetSchemaKindProperty(string kind, Type valueType)
    {
        foreach (Type propType in GetSchemaKindProperties(kind))
        {
            Type? valType = propType.GetGenericBaseType(typeof(Property<>));
            if (valType != null && valType.GetGenericArguments().FirstOrDefault() == valueType)
                return propType;
        }
        return null;
    }

    /// <inheritdoc/>
    public Type? GetSchemaKindPropertyByName(string kind, string propertyName)
        => GetSchemaKindProperties(kind).FirstOrDefault(propType => propertyName.Equals(propType.GetPropertyName(), StringComparison.OrdinalIgnoreCase));

    #endregion

    #region Node Type

    private readonly ConcurrentDictionary<string, Type> _nodeTypes = new (StringComparer.OrdinalIgnoreCase);
    
    /// <summary>
    /// Register the node type for schema kind
    /// </summary>
    public void RegisterNodeType(string kind, Type nodeType) => _nodeTypes.TryAdd(kind, nodeType);
    
    /// <summary>
    /// Gets the node type for the schema kind
    /// </summary>
    public Type? GetNodeType(string kind) => _nodeTypes.GetValueOrDefault(kind);

    #endregion
        
    #region System Node Schema

    private readonly ConcurrentDictionary<Type, string> _typeCache = new();
    private readonly ConcurrentDictionary<string, string> _arrayCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly NodeSchema _rootSchema = new()
    {
        Name = "",
        Kind = SCHEMA_KIND_NAMESPACE,
        Schemas = [],
    };

    /// <summary>
    /// Gets system schema from C# type
    /// </summary>
    /// <param name="type">The C# type</param>
    /// <returns></returns>
    public string? GetTypeSchema(Type type) => _typeCache.GetValueOrDefault(type);

    /// <summary>
    /// Save a node schema as system-defined schema
    /// </summary>
    internal void SaveSystemSchema(NodeSchema schema)
    {
        // special for array
        if (schema.Kind == SCHEMA_KIND_ARRAY && schema.GetProperty<ArrayProperty>()?.Value is {} arraySchema)
            _arrayCache[arraySchema.Element] = schema.FullName;

        string schemaName = schema.FullName.ToLowerInvariant();
        NodeSchema root = _rootSchema;
        string fullPath = "";

        SpanReader reader = schemaName;
        while(reader.NextNamespace())
        {
            string ns = fullPath;
            string part = reader.Current.ToString();
            fullPath = !string.IsNullOrWhiteSpace(fullPath) ? $"{fullPath}.{part}" : part;

            NodeSchema? node = root.Schemas?.FirstOrDefault(x => x.Name == fullPath);
            if (node == null)
            {
                if (schemaName == fullPath)
                {
                    // Target node: add it
                    root.Schemas = root.Schemas != null ? root.Schemas.Concat([schema]).ToArray() : [schema];
                }
                else
                {
                    // Intermediate namespace: create it
                    node = new NodeSchema
                    {
                        Name = part,
                        Namespace = ns,
                        Kind = SCHEMA_KIND_NAMESPACE,
                        Schemas = [],
                    };
                    node.SetProperty<Display, LocaleString>(node.FullName);
                    root.Schemas = root.Schemas != null ? root.Schemas.Concat([node]).ToArray() : [node];
                    root = node;
                    root.Schemas ??= [];
                }
            }
            else if (schemaName != fullPath)
            {
                root = node;
                root.Schemas ??= [];
            }
            else if (node.Kind != schema.Kind || node.Type != null && schema.Type != null && node.Type != schema.Type)
            {
                // Conflict with existing schema
                throw new InvalidOperationException($"System schema name conflict: {schema.FullName} with kind {schema.Kind} conflicts with existing kind {node.Kind}");
            }
            // override the extension properties
            else if (node.Kind != SCHEMA_KIND_NAMESPACE)
            {
                node.CombineExtensions(schema, this);
            }
        }
        
        // Cache the type to name mapping for quick lookup
        if (schema.Type != null)
            _typeCache.TryAdd(schema.Type, schemaName);
        if (schema.Equivalents == null) return;
        foreach (Type eq in schema.Equivalents)
            _typeCache[eq] = schemaName;
    }

    /// <summary>
    /// Gets a system-defined node schema by name
    /// </summary>
    public NodeSchema? GetSystemSchema(string schemaName)
    {
        NodeSchema? node = _rootSchema;
        SpanReader reader = schemaName;
        while (node != null && reader.NextNamespace())
        {
            ReadOnlySpan<char> part = reader.Current;
            NodeSchema? curr = null;
            if (node.Schemas != null)
            {
                foreach (NodeSchema schema in node.Schemas)
                {
                    if (!part.Equals(schema.Name, StringComparison.OrdinalIgnoreCase)) continue;
                    curr = schema;
                    break;
                }
            }
            node = curr;
        }

        return node?.Clone(this);
    }
    
    /// <summary>
    /// Try gets the array schema for the given element type. The element type should be the full name of the type, e.g. "system.string" for string array.
    /// </summary>
    public string GetSystemArraySchema(string elementType) => 
        _arrayCache.GetValueOrDefault(elementType) ?? $"{NS_SYSTEM_LIST}<{elementType.ToLowerInvariant()}>";
    
    #endregion

    #region Node Types
    
    /// <summary>
    /// The root namespace
    /// </summary>
    public readonly NamespaceType RootNamespace = new ();
    
    #endregion
}