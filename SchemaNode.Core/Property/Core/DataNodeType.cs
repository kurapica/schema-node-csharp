using SchemaNode.Runtime;

namespace SchemaNode.Property.Core;

/// <summary>
/// Declares the data node type used to create the runtime data node for a schema type or a schema field
/// </summary>
public class DataNodeType : Property<Type>
{
    /// <inheritdoc/>
    public override void SetValue<TValue>(TValue value)
    {
        if (value is not Type nodeType || !typeof(IValueAccess).IsAssignableFrom(nodeType))
            throw new InvalidOperationException($"The data node type must be a type implementing {nameof(IValueAccess)}");
        base.SetValue(nodeType);
    }
}
