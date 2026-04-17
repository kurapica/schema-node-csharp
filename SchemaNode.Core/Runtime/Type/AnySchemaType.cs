using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Property;
using SchemaNode.Property.Schema;
using SchemaNode.Schema;
using SchemaNode.Utility;

namespace SchemaNode.Runtime;

[Meta<AsErrorCode>("no_definition", 1)]
[Meta<AsErrorCode>("wrong_ref_type", 2)]
public abstract class AnySchemaType : IDisposable
{
    #region Fields

    /// <summary>
    /// The schema name
    /// </summary>
    public string Name => Schema.Name;

    /// <summary>
    /// The namespace that holds the type
    /// </summary>
    public NamespaceType? Namespace { get; private set; }

    /// <summary>
    /// The node schema
    /// </summary>
    public required NodeSchema Schema { get; init; }

    /// <summary>
    /// The schema provider used to load the schema type
    /// </summary>
    public Type? SchemaProvider { get; set; }

    /// <summary>
    /// The error code. Null means ready/no error.
    /// Values are dynamically registered via [Meta&lt;AsErrorCode&gt;] on runtime types.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Whether the schema is used
    /// </summary>
    public virtual bool IsUsed => _usedBy is { IsEmpty: false };

    /// <summary>
    /// Whether the type is loaded
    /// </summary>
    internal bool Loaded { get; set; }

    #endregion

    #region Schema Properties

    /// <summary>
    /// The properties
    /// </summary>
    protected IProperty[]? Properties { get; private set; }

    /// <summary>
    /// The constraint properties from Extensions
    /// </summary>
    protected IConstraintProperty[]? Constraints { get; private set; }

    /// <summary>
    /// The ref types from the properties in Extensions
    /// </summary>
    protected List<AnySchemaType>? RefTypes { get; private set; }

    /// <summary>
    /// Gets the property by type
    /// </summary>
    public T? GetProperty<T>() where T : IProperty
        => Properties == null ? default(T?) : Properties.OfType<T>().FirstOrDefault();

    #endregion

    #region Loading

    /// <summary>
    /// Load the type with the schema, including properties, constraints and ref types
    /// </summary>
    public async Task LoadTypeAsync(SchemaContext context, NodeSchema schema, bool preload = false)
    {
        // Clear previous state
        ReleaseType();

        // Parse extension properties from the schema
        if (schema.Extensions is { Count: > 0 })
        {
            List<IProperty> props = [];
            ISchemaRuntime runtime = context.Runtime;
            string kind = schema.Kind?.ToLowerInvariant() ?? "";

            // Get registered property types for this schema kind
            foreach (Type propType in runtime.GetSchemaProperties(kind))
            {
                if (Activator.CreateInstance(propType) is not IProperty prop) continue;

                // Try get property name/alias
                string propName = propType.GetPropertyName();
                if (!schema.Extensions.TryGetValue(propName, out JsonNode? node)) continue;

                prop.SetValue(node);
                if (prop.HasValue) props.Add(prop);
            }

            if (props.Count > 0)
            {
                Properties = props.ToArray();
                Constraints = props.OfType<IConstraintProperty>().ToArray();

                // Resolve type references
                foreach (ITypeRefProperty typeRef in props.OfType<ITypeRefProperty>())
                {
                    if (!typeRef.HasValue) continue;
                    string? name = typeRef.GetValue<string>();
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    AnySchemaType? refNode = await runtime.GetSchemaTypeAsync(context, name);
                    if (refNode != null)
                    {
                        RefTypes ??= [];
                        RefTypes.Add(refNode);
                        refNode.AddRef(this);
                    }
                    else
                    {
                        Error = "wrong_ref_type";
                        context.LogWarning("[Runtime] Failed to load ref type '{Name}' in schema '{SchemaName}'", name, Name);
                    }
                }
            }
        }

        Loaded = true;
        await LoadAsync(context, schema, preload);
    }

    /// <summary>
    /// Type-specific loading logic. Override in subclasses.
    /// </summary>
    public virtual Task LoadAsync(SchemaContext context, NodeSchema schema, bool preload = false)
        => Task.CompletedTask;

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
        Constraints = null;
    }

    #endregion

    #region Reference Tracking

    /// <summary>
    /// Add a reference from another type
    /// </summary>
    public void AddRef(AnySchemaType usedBy)
    {
        _usedBy ??= [];
        _usedBy.Add(usedBy);
    }

    /// <summary>
    /// Remove a reference from another type
    /// </summary>
    public void RemoveRef(AnySchemaType usedBy)
    {
        if (_usedBy == null) return;
        // ConcurrentBag doesn't support removal, rebuild
        var items = _usedBy.Where(x => x != usedBy).ToArray();
        _usedBy = new ConcurrentBag<AnySchemaType>(items);
    }

    /// <summary>
    /// Set the namespace that holds this type
    /// </summary>
    internal void SetNamespace(NamespaceType ns) => Namespace = ns;

    #endregion

    #region Implementation

    public virtual void Dispose()
    {
        ReleaseType();
        _usedBy = null;
    }

    #endregion

    #region Utility

    private ConcurrentBag<AnySchemaType>? _usedBy;

    #endregion
}
