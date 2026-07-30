using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Struct;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Common;

/// <summary>
/// The display property, which is used to specify the display information
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_INT, SCHEMA_KIND_STRING, SCHEMA_KIND_DECIMAL, SCHEMA_KIND_DATE, SCHEMA_KIND_PROPERTY)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_COMMON}.{nameof(Error)}")]
public sealed class Error : Property<LocaleString>
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