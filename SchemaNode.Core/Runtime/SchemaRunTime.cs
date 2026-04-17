using System.Collections.Concurrent;
using SchemaNode.Context;
using SchemaNode.Schema;
using SchemaNode.Service;
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
    #region Schema Kind Registry

    /// <summary>
    /// Register a schema kind mapping: kind string → (schema definition class, runtime type class)
    /// </summary>
    /// <param name="kind">The schema kind string (e.g. "scalarschema")</param>
    /// <param name="schemaType">The schema definition class (e.g. typeof(ScalarSchema))</param>
    /// <param name="runtimeType">The runtime type class (e.g. typeof(ScalarType))</param>
    /// <param name="valueType">The schema value node class (e.g. typeof(ScalarNode))</param>
    /// <param name="order">The loading order for the kind</param>
    public void RegisterSchemaKind(string kind, Type schemaType, Type? runtimeType, Type? valueType, int order = 0)
    {
        kind = kind.ToLowerInvariant();
        _schemaKinds[kind] = new SchemaKindInfo(kind, schemaType, runtimeType, valueType, order);
    }

    /// <summary>
    /// Gets the registered runtime type for a given schema kind
    /// </summary>
    public Type? GetSchemaRuntimeType(string kind)
    {
        kind = kind.ToLowerInvariant();
        return _schemaKinds.TryGetValue(kind, out var info) ? info.RuntimeType : null;
    }

    /// <summary>
    /// Gets the registered value node type for a given schema kind
    /// </summary>
    public Type? GetSchemaValueType(string kind)
    {
        kind = kind.ToLowerInvariant();
        return _schemaKinds.TryGetValue(kind, out var info) ? info.ValueType : null;
    }

    /// <summary>
    /// Gets all registered schema kinds in order
    /// </summary>
    public IEnumerable<string> GetSchemaKinds()
        => _schemaKinds.Values.OrderBy(k => k.Order).Select(k => k.Kind);

    #endregion

    #region Schema Property Registry

    /// <summary>
    /// Register a schema property type with its applicable schema kinds
    /// </summary>
    /// <param name="propertyType">The property type</param>
    /// <param name="forSchemas">The schema kinds this property applies to</param>
    public void RegisterSchemaProperty(Type propertyType, string[] forSchemas)
    {
        foreach (string schema in forSchemas)
        {
            string key = schema.ToLowerInvariant();
            _schemaProperties.GetOrAdd(key, _ => []).Add(propertyType);
        }
    }

    /// <summary>
    /// Gets all registered property types for a given schema kind
    /// </summary>
    public IEnumerable<Type> GetSchemaProperties(string kind)
    {
        kind = kind.ToLowerInvariant();
        return _schemaProperties.TryGetValue(kind, out var types) ? types : [];
    }

    #endregion

    #region System Schema

    /// <summary>
    /// Save a node schema as system-defined schema
    /// </summary>
    public void SaveSystemNodeSchema(NodeSchema schema)
    {
        string schemaName = schema.Name.ToLowerInvariant();
        NodeSchema root = _systemSchemaRoot;
        string fullPath = "";

        foreach (string path in schemaName.SplitTypeName())
        {
            fullPath = !string.IsNullOrWhiteSpace(fullPath) ? $"{fullPath}.{path}" : path;

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
                        Name = fullPath,
                        Kind = nameof(NamespaceSchema).GetSchemaKind(),
                        Schemas = [],
                    };
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
            // else: already exists at this level, skip
        }
    }

    /// <summary>
    /// Gets a system-defined node schema by name
    /// </summary>
    public NodeSchema? GetSystemNodeSchema(string schemaName)
    {
        schemaName = schemaName.ToLowerInvariant();
        NodeSchema? node = _systemSchemaRoot;
        string fullPath = "";

        foreach (string path in schemaName.SplitTypeName())
        {
            fullPath = !string.IsNullOrWhiteSpace(fullPath) ? $"{fullPath}.{path}" : path;
            node = node?.Schemas?.FirstOrDefault(x => x.Name.Equals(fullPath, StringComparison.OrdinalIgnoreCase));
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

    #region Schema Providers

    /// <summary>
    /// Register a schema provider for loading non-system schemas
    /// </summary>
    public void RegisterSchemaProvider(ISchemaProvider provider) => _providers.Add(provider);

    /// <summary>
    /// Gets all registered schema providers
    /// </summary>
    public IEnumerable<ISchemaProvider> SchemaProviders => _providers;

    #endregion

    #region System Type Cache

    /// <summary>
    /// Initialize system type cache after system schemas are loaded
    /// </summary>
    public async Task InitSystemTypesAsync(SchemaContext context)
    {
        // Load root namespace
        await GetSchemaTypeAsync(context, "", preload: true);
        ResetTypeNamespace();

        // Cache system basic types
        SystemBool = await GetSchemaTypeAsync<ScalarType>(context, NS_SYSTEM_BOOL);
        SystemInt = await GetSchemaTypeAsync<ScalarType>(context, NS_SYSTEM_INT);
        SystemString = await GetSchemaTypeAsync<ScalarType>(context, NS_SYSTEM_STRING);
        SystemDate = await GetSchemaTypeAsync<ScalarType>(context, NS_SYSTEM_DATE);
        SystemGuid = await GetSchemaTypeAsync<ScalarType>(context, NS_SYSTEM_GUID);
        SystemList = await GetSchemaTypeAsync<ArrayType>(context, NS_SYSTEM_LIST);
        SystemProperty = await GetSchemaTypeAsync<NamespaceType>(context, NS_SYSTEM_SCHEMA_PROPERTY);

        context.LogInformation("[Runtime] System types initialized");
    }

    public ScalarType? SystemBool { get; private set; }
    public ScalarType? SystemInt { get; private set; }
    public ScalarType? SystemString { get; private set; }
    public ScalarType? SystemDate { get; private set; }
    public ScalarType? SystemGuid { get; private set; }
    public ArrayType? SystemList { get; private set; }
    public NamespaceType? SystemProperty { get; private set; }

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
            Kind = nameof(NamespaceSchema).GetSchemaKind(),
        }
    };

    private readonly NodeSchema _systemSchemaRoot = new()
    {
        Name = "",
        Kind = nameof(NamespaceSchema).GetSchemaKind(),
        Schemas = [],
    };

    private readonly ConcurrentDictionary<string, SchemaKindInfo> _schemaKinds = new();
    private readonly ConcurrentDictionary<string, ConcurrentBag<Type>> _schemaProperties = new();
    private readonly List<ISchemaProvider> _providers = [];

    #endregion

    #region Inner Types
    
    /// <summary>
    /// Represents a registered schema kind with its metadata
    /// </summary>
    record SchemaKindInfo(string Kind, Type SchemaType, Type? RuntimeType, Type? ValueType, int Order);

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