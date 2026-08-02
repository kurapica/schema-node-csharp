using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Struct;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Common;

/// <summary>
/// The unit label
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_INT, SCHEMA_KIND_DECIMAL, SCHEMA_KIND_STRING)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_COMMON}.{nameof(Unit)}")]
public class Unit : Property<LocaleString>
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