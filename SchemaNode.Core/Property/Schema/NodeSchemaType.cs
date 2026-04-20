using SchemaNode.Runtime;

namespace SchemaNode.Property.Schema;

/// <summary>
/// The runtime type for node schema
/// </summary>
public class NodeSchemaType : Property<Type>
{
    public override void SetValue<TValue>(TValue value)
    {
        base.SetValue(value is Type type && type.IsAssignableTo(typeof(AnySchemaType)) ? type : null);
    }
}