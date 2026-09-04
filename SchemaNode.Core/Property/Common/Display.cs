using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using SchemaNode.Property.Property;
using SchemaNode.Runtime;
using SchemaNode.Struct;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Common;

/// <summary>
/// The display property, which is used to specify the display information
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_COMMON}.{nameof(Display)}")]
public class Display : Property<LocaleString>
{
    /// <inheritdoc/>
    public override void SetValue<TValue>(TValue value)
    {
        base.SetValue(value);
        Locale.Translate(Value);
    }

    /// <inheritdoc/>
    public override bool Combine(IProperty other, ISchemaRuntime? runtime = null)
    {
        if (other.GetValue<LocaleString>() is not { } otherValue) return false;
        if (Value == null)
        {
            SetValue(otherValue);
            return true;
        }
        Value.Concat(otherValue);
        return true;
    }
}