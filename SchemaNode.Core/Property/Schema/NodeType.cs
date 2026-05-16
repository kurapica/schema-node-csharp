namespace SchemaNode.Property.Schema;

/// <summary>
/// The runtime type for node schema
/// </summary>
public class NodeType : Property<Type>
{
    public override void SetValue<TValue>(TValue value)
    {
        base.SetValue(value is Type type && type.IsAssignableTo(typeof(Runtime.NodeType)) ? type : null);
    }
}