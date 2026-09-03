using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property.Core;
using SchemaNode.Property.Property;
using SchemaNode.Struct;
using static SchemaNode.Utility.Constant;
using EnumType = SchemaNode.Runtime.EnumType;

namespace SchemaNode.Property.Enum;

/// <summary>
/// Limit the enum's cascade level
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_ENUM, SCHEMA_KIND_ENUM_USAGE)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_ENUM}.{nameof(Cascade)}")]
public class Cascade : Property<long>, IConstraintProperty
{
    public async Task<bool?> ValidateEnumAsync(SchemaContext context, EnumNode node)
    {
        var effectiveValue = Value;
        if (effectiveValue <= 0 || node.IsEmpty) return null;
        EnumType? enumType = node.Type as EnumType;
        if (enumType?.Cascade == null || enumType.Cascade.Length <= effectiveValue) return null;

        EntryAccess<string>[] access = await enumType.GetEnumEntryAccessAsync(context, node.GetValue<string>());
        return access.Length <= effectiveValue;
    }
}