using SchemaNode.Context;
using SchemaNode.Property.Common;
using SchemaNode.Runtime;

namespace SchemaNode.Node;

/// <summary>
/// The function argument node
/// </summary>
public class FuncArgNode : StructNode
{
    public FuncArgNode(ISchemaContext context, IValueTypeAccess type, object? value = null, IValueAccess? parent = null, params IPropertyProvider[] propertyProviders)
        : base(context, type as StructType ?? throw new InvalidCastException($"The type '{type.Name}' is not a valid function argument type."), value, parent, propertyProviders)
    {
    }

    /// <summary>
    /// Set the argument unmodifiable, the name and type will be read only
    /// </summary>
    public void SetUnModifiable(bool value)
    {
        if (!FullMode) return;
        IValueAccess target = Parent ?? this;
        bool flag = value;
        GetAccessValue("name")?.SetPropertyValue<ReadOnly, bool>(flag, target, null);
        GetAccessValue("type")?.SetPropertyValue<ReadOnly, bool>(flag, target, null);
    }
}
