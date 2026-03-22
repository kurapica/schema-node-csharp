namespace SchemaNode.Components.Property.Presentation;

public interface IPresentation: IProperty
{
}

/// <summary>
/// The abstract class for presentation components that can be attached to schemas, such like format, display, etc.
/// </summary>
public abstract class Presentation<T> : IProperty<T>, IPresentation
{
    /// <summary>
    /// The presentation name
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// The presentation value
    /// </summary>
    public T? Value { get; set; }
}
