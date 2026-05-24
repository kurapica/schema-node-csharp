namespace SchemaNode.Property;

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