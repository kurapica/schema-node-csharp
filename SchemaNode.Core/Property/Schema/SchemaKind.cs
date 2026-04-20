using SchemaNode.Utility;

namespace SchemaNode.Property.Schema;

/// <summary>
/// Declare a new schema kind
/// </summary>
public class SchemaKind : OrderProperty<string>
{
    public override void SetValue<TValue>(TValue value)
    {
        base.SetValue(value.TryConvertTo<string>()?.GetSchemaKind());
    }
}