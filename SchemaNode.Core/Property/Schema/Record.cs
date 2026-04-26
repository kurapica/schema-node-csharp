using SchemaNode.Utility;

namespace SchemaNode.Property.Schema;

/// <summary>
/// Register Record property value for the enum type
/// </summary>
public class Record : Property<Type>
{
    public override void SetValue<TValue>(TValue value)
    {
        base.SetValue(value is Type type && type.IsSubclassOfGenericType(typeof(RecordProperty<>)) ? type : throw new InvalidCastException("Type must be RecordProperty<>"));
    }
}