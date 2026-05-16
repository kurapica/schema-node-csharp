using SchemaNode.Node;

namespace SchemaNode.Property.Schema;

/// <summary>
/// The runtime data node type for node schema
/// </summary>
public class ValueType : Property<Type>
{
    public override void SetValue<TValue>(TValue value)
    {
        base.SetValue(value is Type type && type.IsAssignableTo(typeof(IDataNode)) ? type : null);
    }
}