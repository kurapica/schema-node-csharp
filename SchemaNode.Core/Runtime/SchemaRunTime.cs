using System.Collections.Concurrent;
using System.Reflection;
using SchemaNode.Context;
using SchemaNode.Property;
using SchemaNode.Property.Presentation;
using SchemaNode.Schema;
using SchemaNode.Service;
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

    /// <inheritdoc/>
    public void RegisterSchemaKind(string kind, Type schemaType, Type[]? properties = null)
        => _schemaKinds = _schemaKinds.Append((kind, schemaType, properties)).ToArray();

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

    #endregion

    #region System Schema

    private readonly ConcurrentDictionary<Type, string> _typeCache = new();
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
        string schemaName = schema.FullName.ToLowerInvariant();
        NodeSchema root = _rootSchema;
        string fullPath = "";

        foreach (string part in schemaName.SplitTypeName())
        {
            string ns = fullPath;
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
                node.CombineExtensions(schema);
            }
        }
        
        // Cache the type to name mapping for quick lookup
        if (schema.Type != null)
            _typeCache.TryAdd(schema.Type, schemaName);
        if (schema.Equivalents != null)
        {
            foreach (Type eq in schema.Equivalents)
                _typeCache.TryAdd(eq, schemaName);
        }
    }

    /// <summary>
    /// Gets a system-defined node schema by name
    /// </summary>
    public NodeSchema? GetSystemSchema(string schemaName)
    {
        NodeSchema? node = _rootSchema;

        foreach (string part in schemaName.SplitTypeName())
        {
            node = node.Schemas?.FirstOrDefault(x => x.Name.Equals(part, StringComparison.OrdinalIgnoreCase));
            if (node == null) return null;
        }

        return node;
    }

    #endregion

    #region Schema Type Resolution

    /// <summary>
    /// Gets the schema type by name
    /// </summary>
    /// <param name="context"></param>
    /// <param name="schemaName"></param>
    /// <param name="reload"></param>
    /// <param name="preload"></param>
    /// <returns></returns>
    public async Task<AnySchemaType?> GetSchemaTypeAsync(SchemaContext context, string schemaName, bool reload = false, bool preload = false)
    {
        if (string.IsNullOrWhiteSpace(schemaName) && _root.Loaded)
            return _root;

        return await GetSchemaTypeAsync(context, _root, schemaName.ToLowerInvariant().SplitTypeName(), reload, preload);
    }

    /// <summary>
    /// Gets the schema type by name with given C# type
    /// </summary>
    /// <param name="context"></param>
    /// <param name="schemaName"></param>
    /// <param name="reload"></param>
    /// <param name="preload"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public async Task<T?> GetSchemaTypeAsync<T>(SchemaContext context, string schemaName, bool reload = false, bool preload = false) where T : AnySchemaType
        => await GetSchemaTypeAsync(context, schemaName, reload, preload) as T;

    /// <summary>
    /// Remove a schema
    /// </summary>
    /// <param name="schemaName"></param>
    /// <returns></returns>
    public bool RemoveSchemaType(string schemaName)
    {
        AnySchemaType? node = _root;
        if (string.IsNullOrWhiteSpace(schemaName)) return false;

        string[] paths = schemaName.SplitTypeName();
        foreach (string path in paths.SkipLast(1))
        {
            if (node is not NamespaceType parent || !parent.SchemaNodes.TryGetValue(path, out node))
                return false;
        }

        if (node is NamespaceType ns)
        {
            if (ns.SchemaNodes.TryGetValue(paths.Last(), out AnySchemaType? child))
            {
                if (child.IsUsed) return false;
                ns.SchemaNodes.TryRemove(paths.Last(), out _);
                child.Dispose();
            }
            ns.Schemas = ns.Schemas.Where(s => !s.Name.Equals(schemaName, StringComparison.OrdinalIgnoreCase)).ToArray();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Reset namespaces tobe reloaded again
    /// </summary>
    /// <param name="root"></param>
    public void ResetTypeNamespace(NamespaceType? root = null)
    {
        root ??= _root;
        root.Loaded = false;
        foreach (NamespaceType ns in root.SchemaNodes.Values.OfType<NamespaceType>())
            ResetTypeNamespace(ns);
    }

    #endregion

    #region Factory

    /// <summary>
    /// Create an AnySchemaType instance from a NodeSchema using the schema kind registry
    /// </summary>
    public AnySchemaType? CreateSchemaType(NodeSchema schema)
    {
        string kind = schema.Kind?.ToLowerInvariant() ?? "";
        if (!_schemaKinds.TryGetValue(kind, out var info)) return null;

        AnySchemaType instance = (AnySchemaType)Activator.CreateInstance(info.RuntimeType)!;
        // Set the Schema via reflection since it's required init-only
        typeof(AnySchemaType).GetProperty(nameof(AnySchemaType.Schema))!.SetValue(instance, schema);
        return instance;
    }

    #endregion

    #region Internal: Schema Loading Pipeline

    /// <summary>
    /// Navigate the namespace tree to find/load a schema type
    /// </summary>
    async Task<AnySchemaType?> GetSchemaTypeAsync(SchemaContext context, NamespaceType node, string[] paths, bool reload, bool preload)
    {
        string path = paths.Length > 0 ? paths[0] : string.Empty;

        // Try get sub node
        AnySchemaType? subNode = paths.Length == 0 ? node : node.SchemaNodes.GetValueOrDefault(path);
        NodeSchema? nodeSchema = null;
        string schemaName = subNode == _root
            ? ""
            : node != _root
                ? $"{node.Name}.{path}"
                : path;

        // Init if not exist
        if (subNode == null)
        {
            context.LogDebug("[Runtime] Schema Type {SchemaName} loading", schemaName);
            nodeSchema = await LoadSchemaAsync(context, schemaName);
            if (nodeSchema == null) return null;
            subNode = InitSchemaType(node, nodeSchema);
        }

        if (!subNode.Loaded || (reload && paths.Length <= 1))
        {
            // Avoid cyclic loading
            subNode.Loaded = true;

            context.LogDebug("[Runtime] Schema Type {SchemaName} loading", schemaName);

            // Re-load schema for full definition
            nodeSchema ??= await LoadSchemaAsync(context, schemaName);
            if (nodeSchema == null)
            {
                context.LogWarning("[Runtime] Schema Type {SchemaName} load failed", schemaName);
                return null;
            }

            // Load the type
            subNode.ReleaseType();
            subNode.Error = null;
            await subNode.LoadTypeAsync(context, nodeSchema, preload);

            context.LogDebug("[Runtime] Schema Type {SchemaName} working", schemaName);
        }

        // Navigate deeper or return
        return paths.Length <= 1
            ? subNode
            : subNode is NamespaceType subNs && paths.Length > 1
                ? await GetSchemaTypeAsync(context, subNs, paths.Skip(1).ToArray(), reload, preload)
                : null;
    }

    /// <summary>
    /// Initialize a schema type from a NodeSchema and add it to the parent namespace
    /// </summary>
    AnySchemaType InitSchemaType(NamespaceType root, NodeSchema schema)
    {
        AnySchemaType? schemaType = CreateSchemaType(schema);
        if (schemaType == null)
        {
            // Fallback: treat as namespace if kind not found
            schemaType = new NamespaceType { Schema = schema };
        }

        string localName = schema.Name.SplitTypeName().Last();
        root.SchemaNodes[localName] = schemaType;
        schemaType.SetNamespace(root);

        if (Array.FindIndex(root.Schemas, s => s.Name.Equals(schema.Name, StringComparison.OrdinalIgnoreCase)) < 0)
            root.Schemas = root.Schemas.Append(schema).ToArray();

        // Recursively init nested namespaces
        if (schemaType is NamespaceType ns && schema.Schemas != null)
        {
            foreach (NodeSchema sub in schema.Schemas)
                InitSchemaType(ns, sub);
        }

        return schemaType;
    }

    /// <summary>
    /// Load schema from system definitions first, then fall back to providers
    /// </summary>
    async Task<NodeSchema?> LoadSchemaAsync(SchemaContext context, string schemaName)
    {
        // Check system schemas first
        NodeSchema? schema = GetSystemNodeSchema(schemaName);
        if (schema != null) return schema;

        // Fall back to registered providers
        foreach (ISchemaProvider provider in _providers)
        {
            NodeSchema[]? results = await provider.LoadSchemaAsync([schemaName]);
            if (results is { Length: > 0 })
            {
                NodeSchema result = results[0];
                return result;
            }
        }

        // Try to load from context's service provider (scoped providers)
        foreach (ISchemaProvider provider in context.GetServices<ISchemaProvider>())
        {
            NodeSchema[]? results = await provider.LoadSchemaAsync([schemaName]);
            if (results is { Length: > 0 })
                return results[0];
        }

        return null;
    }

    #endregion

    #region Utility

    private readonly NamespaceType _root = new()
    {
        Schema = new NodeSchema
        {
            Name = "",
            Kind = SCHEMA_KIND_NAMESPACE,
        }
    };

    #endregion
}

/// <summary>
/// Schema load state flags
/// </summary>
[Flags]
public enum SchemaLoadState
{
    /// <summary>Server defined</summary>
    Server = 1,
    /// <summary>Custom defined</summary>
    Custom = 2,
    /// <summary>Frontend defined</summary>
    Frontend = 4,
    /// <summary>System defined</summary>
    System = 8,
}