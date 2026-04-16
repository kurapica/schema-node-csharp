using System.Collections.Concurrent;
using SchemaNode.Property;
using SchemaNode.Schema;

namespace SchemaNode.Runtime;

public abstract class AnySchemaType: IDisposable
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
    /// The error message
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
    protected AnySchemaType[]? RefTypes { get; private set; }
    
    /// <summary>
    /// Gets the property by type
    /// </summary>
    public T? GetProperty<T>() where T : IProperty
        => Properties == null ? default(T?) : Properties.OfType<T>().FirstOrDefault();
    
    #endregion
    
    #region Implementation

    public void Dispose()
    {
        throw new NotImplementedException();
    }
    
    #endregion
    
    #region Utility

    private ConcurrentBag<AnySchemaType>? _usedBy;

    #endregion
}
