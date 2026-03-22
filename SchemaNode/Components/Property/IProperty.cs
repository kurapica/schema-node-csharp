namespace SchemaNode.Components;

public interface IProperty
{
    /// <summary>
    /// The property name
    /// </summary>
    string Name { get; internal set; }
}

/// <summary>
/// The base interface for all property components that can be attached to schemas, such like presentation, constraint, etc.
/// </summary>
public interface IProperty<T> : IProperty
{
    /// <summary>
    /// The property value
    /// </summary>
    T? Value { get; internal set; }
}
