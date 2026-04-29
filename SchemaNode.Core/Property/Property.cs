using System.Collections.Concurrent;
using SchemaNode.Function;
using SchemaNode.Utility;

namespace SchemaNode.Property;

/// <summary>
/// Represents a property with name and value
/// </summary>
public interface IProperty
{
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
}

/// <summary>
/// The property with value of type T
/// </summary>
public abstract class Property<T> : IProperty
{
    /// <summary>
    /// The property value
    /// </summary>
    public T? Value { get; protected set; }

    /// <summary>
    /// Sets the property value
    /// </summary>
    public virtual void SetValue<TValue>(TValue value) => Value = value.TryConvertTo<T>();

    /// <summary>
    /// Gets the value
    /// </summary>
    public virtual TV? GetValue<TV>(bool matchType = false) => HasValue && (!matchType || Value is TV) ? Value.TryConvertTo<TV>() : default(TV?);

    /// <summary>
    /// Check the value is not empty
    /// </summary>
    public virtual bool HasValue => !SystemLogic.isempty(Value);
}

#region Property Owner

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

/// <summary>
/// The property with owner, which can be used to access other properties of the same owner
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class OwnerProperty<T>: Property<T>
{
    /// <summary>
    /// The property owner
    /// </summary>
    public IPropertyOwner? Owner { get; set; }
}

#endregion

#region Readonly

/// <summary>
/// Readonly property with owner, their value is generated from other properties of the same owner
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class ReadOnlyOwnerProperty<T> : OwnerProperty<T>
{
    // ignore the set value
    public override void SetValue<TValue>(TValue value) { }
}

#endregion

#region Order Property

/// <summary>
/// The order property
/// </summary>
public interface IOrderProperty : IProperty
{
    int Order { get; set; }
}

/// <summary>
/// The property with order
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class OrderProperty<T> : Property<T>, IOrderProperty
{
    /// <summary>
    /// The property order
    /// </summary>
    public int Order { get; set; }
}

#endregion

#region Record Property

/// <summary>
/// The record property, which will be recorded in the static dictionary when set value, and can be accessed by type and order
/// </summary>
public abstract class RecordProperty<T> : OrderProperty<T>
{
    /// <inheritdoc/>
    public override void SetValue<TValue>(TValue value)
    {
        base.SetValue(value);
        this.Record();
    }
}

public static class PropertyExtensions
{
    private static readonly ConcurrentDictionary<Type, ConcurrentBag<IOrderProperty>> Records = [];

    /// <summary>
    /// Record the property
    /// </summary>
    /// <param name="property"></param>
    internal static void Record<T>(this RecordProperty<T> property)
        => Records.GetOrAdd(property.GetType(), _ => []).Add(property);
    
    /// <summary>
    /// Gets the order properties from record
    /// </summary>
    public static IEnumerable<IOrderProperty> GetProperties(this Type propertyType)
        => Records.TryGetValue(propertyType, out var properties) 
            ? properties.OrderBy(p => p.Order) 
            : Enumerable.Empty<IOrderProperty>();

    /// <summary>
    /// Gets the recorded values for a given RecordProperty type.
    /// Use this instead of GetProperties when the target type conflicts with Type.GetProperties().
    /// </summary>
    public static IEnumerable<IOrderProperty> GetRecordedValues(this Type propertyType)
        => Records.TryGetValue(propertyType, out var properties) 
            ? properties.OrderBy(p => p.Order) 
            : Enumerable.Empty<IOrderProperty>();
}

#endregion