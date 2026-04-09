using SchemaNode.Attribute;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Schema;

/// <summary>
/// Delcare a new schema kind or as value constraint
/// </summary>
[Meta<Enum>(NS_SYSTEM_SCHEMA_KIND)]
public sealed class SchemaKind : Property<string>
{
    public new void SetValue<T>(T value)
    {
        base.SetValue(value);
        Value = Value?.GetSchemaKind() ?? throw new Exception("The schema kind must be specified");
    }
}