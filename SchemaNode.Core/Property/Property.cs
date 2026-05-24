using System.Collections.Concurrent;
using System.Collections.Immutable;
using SchemaNode.Attribute;
using SchemaNode.Function;
using SchemaNode.Property.Core;
using SchemaNode.Utility;

namespace SchemaNode.Property;

/// <summary>
/// Represents a property with name and value
/// </summary>
public interface IProperty
{
    private static readonly ConcurrentDictionary<Type, string> _names = [];
    private static readonly ConcurrentDictionary<Type, ImmutableArray<string>> _depends = [];
    private static readonly ConcurrentDictionary<Type, ImmutableArray<string>> _overrides = [];
    private static readonly ConcurrentDictionary<Type, ImmutableArray<string>> _forTypes = [];

    private static ImmutableArray<string> GetOverrides(Type propertyType)
        => _overrides.GetOrAdd(propertyType, static t => t.GetMetaProperty<Override>()?.Value?.SelectMany(v => GetOverrides(v).Concat([v.GetPropertyName()])).ToImmutableArray() ?? []);
    
    /// <summary>
    /// Gets the property name
    /// </summary>
    public string Name => _names.GetOrAdd(GetType(), static t => t.GetPropertyName());
    
    /// <summary>
    /// Gets the depend properties
    /// </summary>
    public ImmutableArray<string> Depends => _depends.GetOrAdd(GetType(), static t => t.GetMetaProperty<Depend>()?.Value?.Select(v => v.GetPropertyName()).ToImmutableArray() ?? []);
    
    /// <summary>
    /// Gets the override properties
    /// </summary>
    public ImmutableArray<string> Overrides => GetOverrides(GetType());
    
    /// <summary>
    /// Gets the for types
    /// </summary>
    public ImmutableArray<string> ForTypes => _forTypes.GetOrAdd(GetType(), static t => t.GetMetaProperty<ForType>()?.Value?.ToImmutableArray() ?? []);
    
    /// <summary>
    /// The property has value
    /// </summary>
    bool HasValue { get; }

    /// <summary>
    /// Sets the property value
    /// </summary>
    void SetValue<T>(T value);

    /// <summary>
    /// Gets the property value
    /// </summary>
    T? GetValue<T>(bool matchType = false);
    
    /// <summary>
    /// The property value type
    /// </summary>
    Type Type { get; }
}

/// <summary>
/// The property with value of type T
/// </summary>
public abstract class Property<T> : IProperty
{
    /// <summary>
    /// The property value
    /// </summary>
    public T? Value { get; private set; }

    /// <summary>
    /// Sets the property value
    /// </summary>
    public virtual void SetValue<TValue>(TValue value) => Value = value.ConvertTo<T>();

    /// <summary>
    /// Gets the value
    /// </summary>
    public virtual TV? GetValue<TV>(bool matchType = false) => HasValue && (!matchType || Value is TV) ? Value.ConvertTo<TV>() : default(TV?);

    /// <summary>
    /// Check the value is not empty
    /// </summary>
    public virtual bool HasValue => !SystemLogic.isempty(Value);
    
    /// <summary>
    /// The property value type
    /// </summary>
    public Type Type => typeof(T);
}

/// <summary>
/// The interface for property owner, which can hold multiple properties.
/// </summary>
public interface IPropertyOwner
{
    /// <summary>
    /// Gets the property by type
    /// </summary>
    IProperty? GetProperty(Type type);
    
    /// <summary>
    /// Remove the property
    /// </summary>
    void RemoveProperty(Type type);
    
    /// <summary>
    /// Gets the property by type
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    T? GetProperty<T>() where T : IProperty, new();

    /// <summary>
    /// Sets the property and return itself
    /// </summary>
    void SetProperty(IProperty property);

    /// <summary>
    /// Set the property with type and return itself
    /// </summary>
    void SetProperty<TK, TV>(TV value) where TK : Property<TV>, new();

    /// <summary>
    /// Sets the property with the given property type and value
    /// </summary>
    void SetProperty<T>(Type type, T value);

    /// <summary>
    /// Remove a property
    /// </summary>
    void RemoveProperty<T>() where T: IProperty;
}
