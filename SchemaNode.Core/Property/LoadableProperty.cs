using SchemaNode.Context;

namespace SchemaNode.Property;

/// <summary>
/// Loadable property
/// </summary>
public interface ILoadableProperty
{
    /// <summary>
    /// Process loading task
    /// </summary>
    Task LoadAsync(SchemaContext context);
}
