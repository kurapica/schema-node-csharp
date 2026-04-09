using SchemaNode.Attribute;
using SchemaNode.Property;
using SchemaNode.Property.Schema;

namespace SchemaNode.Schema;

/// <summary>
/// The namespace schema, used as container for other schema nodes
/// </summary>
[Meta<SchemaKind>(nameof(NamespaceSchema))]
public sealed class NamespaceSchema : ExtensibleSchema;

/// <summary>
/// The namespace for schema node
/// </summary>
[Meta<ForSchema>(nameof(NodeSchema))]
public sealed class Namespace : Property<string>
{
    public new void SetValue<T>(T value)
    {
        base.SetValue(value);
        Value = Value?.TrimEnd('.')?.ToLower() ?? throw new Exception("The namespace must be specified");
    }
}


[Meta<ForSchema>(nameof(NodeSchema))]
public sealed class FullName : Property<string>
{
    public new void SetValue<T>(T value)
    {
    }


}