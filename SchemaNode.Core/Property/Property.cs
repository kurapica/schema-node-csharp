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
    private static readonly ConcurrentDictionary<Type, bool> _stackable = [];

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
    /// Whether the property is stackable, which means the property from different sources can be used together.
    /// For example, for the same name constraint property, 
    /// if stackable, all the constraints will be checked, the data isn't valid if any constraint check falied, 
    /// if not stackable, the constraints result will override the previous
    /// </summary>
    public bool Stackable => _stackable.GetOrAdd(GetType(), static t => t.GetMetaProperty<Stackable>()?.Value ?? false);

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
    /// Gets the properties by type, normally for stackable properties
    /// </summary>
    IEnumerable<IProperty> GetProperties(Type type);

    /// <summary>
    /// Gets the property by type
    /// </summary>
    public T? GetProperty<T>() where T : class, IProperty => GetProperty(typeof(T)) as T;
    
    /// <summary>
    /// Gets the property by type
    /// </summary>
    public IEnumerable<T> GetProperties<T>() where T : class, IProperty => GetProperties(typeof(T)).Cast<T>();

    /// <summary>
    /// Sets the property and return itself
    /// </summary>
    void SetProperty(IProperty property);

    /// <summary>
    /// Set the property with type and return itself
    /// </summary>
    public void SetProperty<TK, TV>(TV value) where TK : Property<TV>, new()
    {
        IProperty property = Activator.CreateInstance<TK>();
        property.SetValue(value);
        SetProperty(property);
    }

    /// <summary>
    /// Sets the property with the given property type and value
    /// </summary>
    public void SetProperty<T>(Type type, T value)
    {
        if (Activator.CreateInstance(type) is not IProperty prop) return;
        prop.SetValue(value);
        SetProperty(prop);
    }
    
    /// <summary>
    /// Gets properties from the given types. The properties will be returned in the order of the given types. If there are duplicate properties, the properties from the later types will overwrite the previous ones. If a property has dependencies, it will only be returned when all its dependencies are satisfied. If a property has overrides, it will override the properties with the same name from the previous types.
    /// </summary>
    public List<IProperty> GetProperties(IEnumerable<Type> types)
    {
        List<IProperty> props = [];
        foreach (Type type in types)
        {
            IProperty? prop = GetProperty(type);
            if (prop is not { HasValue: true }) continue;
            if (prop.Depends is { Length: > 0 } depends && depends.Any(d => props.All(p => !p.Name.Equals(d, StringComparison.OrdinalIgnoreCase)))) continue;
            if (prop.Overrides is { Length: > 0 } overrides) props = props.Where(p => !overrides.Any(o => o.Equals(p.Name, StringComparison.OrdinalIgnoreCase))).ToList();
            props.Add(prop);
        }
        return props;
    }
}
