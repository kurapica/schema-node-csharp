using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using SchemaNode.Struct;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Common;

/// <summary>
/// The unit property, which is used to specify the unit information
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_STRUCT_FIELD, SCHEMA_KIND_INT, SCHEMA_KIND_DECIMAL, SCHEMA_KIND_STRING)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_COMMON}.{nameof(Unit)}")]
public sealed class Unit : Property<LocaleString>
{
    /// <inheritdoc/>
    public override void SetValue<TValue>(TValue value)
    {
        base.SetValue(value);
        Locale.Translate(Value);
    }
}