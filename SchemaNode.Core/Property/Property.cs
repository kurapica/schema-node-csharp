using System.Collections.Concurrent;
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
    private static readonly ConcurrentDictionary<Type, bool> _stackable = [];
    private static readonly ConcurrentDictionary<Type, bool> _static = [];

    /// <summary>
    /// Gets the property name
    /// </summary>
    public string Name => _names.GetOrAdd(GetType(), static t => t.GetPropertyName());
    
    /// <summary>
    /// Whether the property is stackable, which means the property from different sources can be used together.
    /// For example, for the same name constraint property, 
    /// if stackable, all the constraints will be checked, the data isn't valid if any constraint check falied, 
    /// if not stackable, the constraints result will override the previous
    /// </summary>
    public bool Stackable => _stackable.GetOrAdd(GetType(), static t => t.GetMetaProperty<Stackable>()?.Value ?? false);

    /// <summary>
    /// Whether the property is static, which means the property value cannot be modified by relation system.
    /// </summary>
    public bool Static => _static.GetOrAdd(GetType(), static t => t.GetMetaProperty<Static>()?.Value ?? false);

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

    /// <summary>
    /// Combine the property with another property of the same type, if the current property has no value, it will take the value from the other property.
    /// If return true means the other property is combined into the current property.
    /// If return false and the property is stackable, the other property can be used together with the current property.
    /// </summary>
    bool Combine(IProperty other);
    
    /// <summary>
    /// Whether the properties are equal, used for stackable property, if the properties are equal, the other property will be ignored.
    /// </summary>
    bool Equals(IProperty other);
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

    /// <inheritdoc/>
    public virtual bool Combine(IProperty other)
    {
        if (other.GetType() != GetType()) return false;
        if (HasValue || !other.HasValue) return false;
        SetValue(other.GetValue<object>());
        return true;
    }
    
    /// <inheritdoc/>
    public virtual bool Equals(IProperty other)
    {
        if (other.GetType() != GetType()) return false;
        if (HasValue != other.HasValue) return false;
        return !HasValue || Equals(other.GetValue<object>(), GetValue<object>());
    }
    
    /// <summary>
    /// Check the value is not empty
    /// </summary>
    public virtual bool HasValue => !SystemLogic.isempty(Value);
    
    /// <summary>
    /// The property value type
    /// </summary>
    public Type Type => typeof(T);
}
