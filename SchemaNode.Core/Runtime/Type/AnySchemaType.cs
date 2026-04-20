using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Property.Schema;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

[Meta<ErrorCode>(ERR_NO_DEFINITION, SCHEMA_KIND_ORDER_NODE * 100 + 1)]
[Meta<ErrorCode>(ERR_WRONG_REF_TYPE, SCHEMA_KIND_ORDER_NODE * 100 + 2)]
public abstract class AnySchemaType : IDisposable
{
    #region Fields

    /// <summary>
    /// The schema full name
    /// </summary>
    public string Name { get; internal set; } = null!;

    /// <summary>
    /// The namespace that holds the type
    /// </summary>
    public NamespaceType? Namespace { get; internal set; }

    /// <summary>
    /// The schema kind
    /// </summary>
    public virtual string Kind => "node";

    /// <summary>
    /// The schema type is a value type
    /// </summary>
    public virtual bool IsValueType => false;

    /// <summary>
    /// The schema is system defined
    /// </summary>
    public bool IsSystem { get; internal set; }

    /// <summary>
    /// The node schema
    /// </summary>
    public NodeSchema? Schema { get; private set; }

    /// <summary>
    /// The schema provider used to load the schema type
    /// </summary>
    public Type? SchemaProvider { get; internal set; }

    /// <summary>
    /// The error code. Null means ready/no error.
    /// Values are dynamically registered via [Meta&lt;AsErrorCode&gt;] on runtime types.
    /// </summary>
    public string? Error { get; internal set; }

    /// <summary>
    /// Whether the schema is used
    /// </summary>
    public virtual bool IsUsed => IsSystem || _usedBy is { IsEmpty: false } ||
                                  _usedByOthers is { IsEmpty: false } && 
                                  _usedByOthers.Values.Any(v => !v.IsEmpty);

    /// <summary>
    /// Whether the type is loaded
    /// </summary>
    internal bool Loaded { get; set; }

    #endregion

    #region Schema Properties

    /// <summary>
    /// The properties
    /// </summary>
    protected IProperty[]? Properties { get; set; }

    /// <summary>
    /// The ref types from the properties in Extensions
    /// </summary>
    protected List<AnySchemaType>? RefTypes { get; set; }

    /// <summary>
    /// Gets the property by type
    /// </summary>
    public T? GetProperty<T>() where T : IProperty
        => Properties == null ? default(T?) : Properties.OfType<T>().FirstOrDefault();

    #endregion

    #region Loading

    /// <summary>
    /// Gets the type schema data
    /// </summary>
    public virtual ExtensibleSchema? GetTypeSchema() => null;

    /// <summary>
    /// Load the type with the schema, including properties, constraints and ref types
    /// </summary>
    public virtual async Task LoadSchemaAsync(SchemaContext context, NodeSchema schema)
    {
        Loaded = true;
        Error = null;
        
        // Clear previous state
        ReleaseType();
        Schema = schema;

        // Loading properties
        ExtensibleSchema? typeSchema = GetTypeSchema();
        if (typeSchema == null) Error = ERR_NO_DEFINITION;
        
        Properties = typeSchema != null 
            ? GetExtensionProperties(typeSchema, context.Runtime.GetSchemaKindProperties(Kind)).ToArray()
            : null;
        if (Properties != null)
        {
            // Resolve type references
            foreach (ITypeRefProperty typeRef in Properties.OfType<ITypeRefProperty>())
            {
                if (!typeRef.HasValue) continue;
                string? name = typeRef.GetValue<string>();
                if (string.IsNullOrWhiteSpace(name)) continue;

                AnySchemaType? refNode = await context.GetSchemaTypeAsync(name);
                if (refNode != null)
                {
                    RefTypes ??= [];
                    RefTypes.Add(refNode);
                    refNode.AddRef(this);
                }
                else
                {
                    Error = ERR_WRONG_REF_TYPE;
                    context.LogWarning("[Runtime] Failed to load ref type '{Name}' in schema '{SchemaName}'", name, Name);
                }
            }
        }
    }

    /// <summary>
    /// Release type-specific data for reload
    /// </summary>
    public virtual void ReleaseType()
    {
        // Remove refs from other types
        if (RefTypes != null)
        {
            foreach (var refType in RefTypes)
                refType.RemoveRef(this);
            RefTypes = null;
        }

        Properties = null;
    }

    #endregion

    #region Reference Tracking

    // Used by other schema types
    private ConcurrentDictionary<AnySchemaType, bool>? _usedBy;
    private ConcurrentDictionary<Type, ConcurrentDictionary<object, bool>>? _usedByOthers;

    /// <summary>
    /// Add a reference from another type
    /// </summary>
    public virtual void AddRef(AnySchemaType usedBy)
    {
        // system types are not tracked
        if (IsSystem) return;
        _usedBy ??= [];
        _usedBy.TryAdd(usedBy, true);
    }

    /// <summary>
    /// Remove a reference from another type
    /// </summary>
    public virtual void RemoveRef(AnySchemaType usedBy) => _usedBy?.TryRemove(usedBy, out _);

    /// <summary>
    /// Add ref for others
    /// </summary>
    public void AddRef<T>(T usedBy)
    {
        _usedByOthers ??= [];
        _usedByOthers.GetOrAdd(typeof(T), _ => []).TryAdd(usedBy!, true);
    }

    /// <summary>
    /// Remove ref for others
    /// </summary>
    public void RemoveRef<T>(T usedBy)
    {
        if (_usedByOthers != null && _usedByOthers.TryGetValue(typeof(T), out var refs))
            refs.TryRemove(typeof(T), out _);
    }

    #endregion

    #region Implementation

    public virtual void Dispose()
    {
        ReleaseType();
        _usedBy = null;
        _usedByOthers = null;
    }

    #endregion

    #region Utility
    
    /// <summary>
    /// Gets the properties with given property types
    /// </summary>
    static IEnumerable<IProperty> GetExtensionProperties(ExtensibleSchema schema, IEnumerable<Type> propertyTypes)
    {
        if (schema.Extensions == null || schema.Extensions.Count == 0) yield break;
        
        foreach (Type propType in propertyTypes)
        {
            string key = propType.GetPropertyName();
            if (!schema.Extensions.TryGetValue(key, out JsonNode? node)) continue;
            if (Activator.CreateInstance(propType) is not IProperty prop) continue;
            prop.SetValue(node);
            if (prop.HasValue)
                yield return prop;
        }
    }

    #endregion
}


/// <summary>
/// The abstract schema type for data schema types, that can be used to validate or generate the data node
/// </summary>
public abstract class ValueSchemaType: AnySchemaType
{
    #region Overrides of AnySchemaType

    private ConcurrentDictionary<ValueSchemaType, FunctionType>? _compatibles;

    /// <inheritdoc/>
    public override async Task LoadSchemaAsync(SchemaContext context, NodeSchema schema)
    {
        await base.LoadSchemaAsync(context, schema);
        
        // Load constraints properties
        Constraints = Properties?.OfType<IConstraintProperty>().ToArray();
    }

    /// <inheritdoc/>
    public override void AddRef(AnySchemaType usedBy)
    {
        // check compatibles, rare but important
        if (IsValueType && usedBy is FunctionType { Args.Length: 1, Converter: true } func &&
            func.Args[0].SchemaType == this && func.ReturnNode is ValueSchemaType valueType && !CanBeUseAs(valueType))
        {
            // Means this type can be converted to func.ReturnNode via func
            _compatibles ??= [];
            _compatibles.TryAdd(valueType, func);
        }

        base.AddRef(usedBy);
    }
    
    /// <inheritdoc/>
    public override void RemoveRef(AnySchemaType usedBy)
    {
        if (usedBy is ValueSchemaType valueType)
            _compatibles?.TryRemove(valueType, out _);
        base.RemoveRef(usedBy);
    }

    /// <inheritdoc/>
    public override bool IsValueType => true;
    
    #endregion
    
    #region Fields
    
    /// <summary>
    /// The constraint properties from Extensions
    /// </summary>
    protected IConstraintProperty[]? Constraints { get; set; }

    /// <summary>
    /// Create the data node by value
    /// </summary>
    public virtual AnySchemaNode? Create(object? value = null) => value is AnySchemaNode node ? node : null;

    /// <summary>
    /// Validate the value with the schema
    /// </summary>
    public virtual Task<AnySchemaNode?> ValidateValueAsync(SchemaContext context, object? value) => Task.FromResult((AnySchemaNode?) null);

    /// <summary>
    /// Whether the schema type can be used as the other
    /// </summary>
    public virtual bool CanBeUseAs(ValueSchemaType other, bool exactly = false)
        => this == other || Name.Equals(other.Name) || Name.Equals(NS_SYSTEM_OBJECT) || Name.Equals(NS_SYSTEM_JSON) ||
           other.Name.Equals(NS_SYSTEM_OBJECT) || other.Name.Equals(NS_SYSTEM_JSON) ||
           !exactly && _compatibles != null && 
           (_compatibles.ContainsKey(other) || _compatibles.Keys.Any(k => k.CanBeUseAs(other, true)));

    /// <summary>
    /// Whether the type can be used as array data index
    /// </summary>
    public virtual bool IsIndexable => false;

    /// <summary>
    /// Whether the value type is array
    /// </summary>
    public virtual bool IsArray => false;
    
    #endregion
}

/// <summary>
/// Represents the schema type support template types
/// </summary>
public interface ITemplateSchemaType;