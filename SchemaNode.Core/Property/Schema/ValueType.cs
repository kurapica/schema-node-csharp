using SchemaNode.Node;

namespace SchemaNode.Property.Schema;

/// <summary>
/// Declare the value node type for the schema kind
/// </summary>
public class ValueType : Property<Type>
{
    public override void SetValue<TValue>(TValue value)
    {
        base.SetValue(value is Type type && type.IsAssignableTo(typeof(AnySchemaNode)) ? type : throw new ArgumentException($"The runtime type must be assignable to {typeof(AnySchemaNode).FullName}."));
    }
}