using SchemaNode.Utility;

namespace SchemaNode.Node;

public abstract class AnySchemaNode
{
    /// <summary>
    /// Convert to type value
    /// </summary>
    public virtual object? ToValue(Type type) => type.TryConvert(_value);

    /// <summary>
    /// Convert to value
    /// </summary>
    public virtual T? ToValue<T>() => ToValue(typeof(T)) is T val ? val : default(T?);

    // The internal value
    protected object? _value;
}
