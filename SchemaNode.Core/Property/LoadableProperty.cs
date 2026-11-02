using SchemaNode.Context;

namespace SchemaNode.Property;

/// <summary>
/// Loadable property
/// </summary>
public interface ILoadableProperty
{
    /// <summary>
    /// CallAsync loading task
    /// </summary>
    Task LoadAsync(SchemaContext context, Runtime.ValueType? ownerType = null);
}
