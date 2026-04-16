using SchemaNode.Attribute;
using SchemaNode.Property.Schema;
using SchemaNode.Schema;
using SchemaNode.Struct;
using SchemaNode.Utility;

namespace SchemaNode.Property.Presentation;

/// <summary>
/// The display property, which is used to specify the display information
/// </summary>
[Meta<ForSchema>(typeof(NodeSchema), typeof(StructFieldSchema))]
public sealed class Display : Property<LocaleString>
{
    /// <inheritdoc/>
    public override void SetValue<TValue>(TValue value)
    {
        base.SetValue(value);
        Locale.Translate(Value);
    }
}