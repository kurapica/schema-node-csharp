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
    T? GetValue<T>();
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
    public virtual TV? GetValue<TV>() => HasValue ? Value.TryConvertTo<TV>() : default(TV?);

    /// <summary>
    /// Check the value is not empty
    /// </summary>
    public virtual bool HasValue => !SystemLogic.isempty(Value);
}

/// <summary>
/// The interface for property owner, which can hold multiple properties.
/// </summary>
public interface IPropertyOwner
{
    /// <summary>
    /// Gets the property by type
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    IProperty? GetProperty<T>() where T : IProperty;

    /// <summary>
    /// Set the property with type
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="property"></param>
    void SetProperty<T>(T property) where T : IProperty;

    /// <summary>
    /// The property with the given type and value, which will be converted to the property type
    /// </summary>
    public void SetProperty<TK, TV>(TV? value) where TK : IProperty, new()
    {
        var property = new TK();
        property.SetValue(value);
        SetProperty(property);
    }

    /// <summary>
    /// Remove the property by type
    /// </summary>
    /// <typeparam name="T"></typeparam>
    void RemoveProperty<T>() where T : IProperty;

    /// <summary>
    /// Gets all properties
    /// </summary>
    /// <returns></returns>
    IEnumerable<IProperty> GetAllProperties();
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

/// <summary>
/// Readonly property with owner, their value is generated from other properties of the same owner
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class ReadOnlyOwnerProperty<T> : OwnerProperty<T>
{
    // ignore the set value
    public override void SetValue<TValue>(TValue value) { }
}