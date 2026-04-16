using SchemaNode.Utility;

namespace SchemaNode.Property.Schema;

/// <summary>
/// Record property value for the given enum type
/// </summary>
public class Record : Property<string>
{
    public override void SetValue<TValue>(TValue value)
    {
        base.SetValue(value is Type type 
            ? (type.IsEnum ? type.GetSchemaType() : null) ?? throw new Exception($"Can't get the schema type of the given type {type.FullName}") 
            : value?.TryConvertTo<string>());
    }
}