using SchemaNode.Context;
using SchemaNode.Runtime;

namespace SchemaNode.Node;

/// <summary>
/// The function call argument node, the value field carries the argument properties
/// </summary>
public class FuncCallArgNode : StructNode
{
    public FuncCallArgNode(ISchemaContext? context, IValueTypeAccess type, object? value = null, IValueAccess? parent = null, params IPropertyProvider[] propertyProviders)
        : base(context, type as StructType ?? throw new InvalidCastException($"The type '{type.Name}' is not a valid function call argument type."), value, parent, propertyProviders)
    {
        // forward the function argument properties to the value field
        if (GetAccessValue("value") is DataNode valueNode)
            valueNode.AddPropertyProvider(propertyProviders.OfType<FunctionNodeArgument>().FirstOrDefault());
    }
}
