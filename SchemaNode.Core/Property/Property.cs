using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Function;
using SchemaNode.Property.Schema;
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

#region Property Owner

/// <summary>
/// The interface for property owner, which can hold multiple properties.
/// </summary>
public interface IPropertyOwner
{
    #region Abstract
    
    /// <summary>
    /// Gets the property by type
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    T? GetProperty<T>() where T : IProperty, new();

    /// <summary>
    /// Set the property with type
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="property"></param>
    void SetProperty<T>(T property) where T : IProperty;

    /// <summary>
    /// Remove a property
    /// </summary>
    /// <param name="property"></param>
    /// <typeparam name="T"></typeparam>
    void RemoveProperty<T>(T property) where T: IProperty;
    
    #endregion
    
    #region Method
    
    /// <summary>
    /// The property with the given type and value, which will be converted to the property type
    /// </summary>
    public void SetProperty<TK, TV>(TV? value) where TK : Property<TV>, new()
    {
        var property = new TK();
        property.SetValue(value);
        SetProperty(property);
    }

    /// <summary>
    /// Remove the property by type
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public void RemoveProperty<T>() where T : IProperty, new()
    {
        var property = GetProperty<T>();
        if (property != null)
            RemoveProperty(property);
    }
    
    #endregion
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
internal interface IOrderProperty : IProperty
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

internal static class RecordPropertyExtensions
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
    internal static IEnumerable<IOrderProperty> GetProperties(this Type propertyType)
        => Records.TryGetValue(propertyType, out var properties) 
            ? properties.OrderBy(p => p.Order) 
            : Enumerable.Empty<IOrderProperty>();
}

#endregion