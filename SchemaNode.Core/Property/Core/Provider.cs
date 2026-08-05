using SchemaNode.Attribute;
using SchemaNode.Property.Common;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Core;

/// <summary>
/// The feature provider with alias name to be used as source
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CORE}.{nameof(Provider)}")]
[Meta<Static>(true)]
[Meta<ReadOnly>(true)]
public class Provider : Property<string>
{
    public override void SetValue<TValue>(TValue value)
    {
        if (value is string str)
            base.SetValue(str.StartsWith('$') ? str : $"${str}");
    }
}