using SchemaNode.Runtime;

namespace SchemaNode.Property.Schema;

/// <summary>
/// The runtime type for schema
/// </summary>
public class RuntimeType : Property<Type>
{
    public override void SetValue<TValue>(TValue value)
    {
        base.SetValue(value is Type type && type.IsAssignableTo(typeof(AnySchemaType)) ? type : throw new ArgumentException($"The runtime type must be assignable to {typeof(AnySchemaType).FullName}."));
    }
}