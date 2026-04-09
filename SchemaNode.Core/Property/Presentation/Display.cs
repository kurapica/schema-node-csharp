using SchemaNode.Struct;
using SchemaNode.Utility;

namespace SchemaNode.Property.Presentation;

/// <summary>
/// The display property, which is used to specify the display name
/// </summary>
public sealed class Display : Property<LocaleString>
{
    public new void SetValue<TValue>(TValue value)
    {
        base.SetValue(value);
        Locale.Translate(Value);
    }
}