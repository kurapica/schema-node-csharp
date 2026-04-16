using SchemaNode.Attribute;
using SchemaNode.Utility;

namespace SchemaNode.Property.Schema;

[Meta<Record>(typeof(Enum.NodeSchemaKind))]
public class AsNodeSchemaKind : RecordProperty<string>
{
    public override void SetValue<TValue>(TValue value)
    {
        base.SetValue(value.TryConvertTo<string>()?.GetSchemaKind());
    }
}