using System.Collections.Concurrent;

namespace SchemaNode.Property;

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
