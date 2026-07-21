using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Constraint;

[Meta<Alias>("enum")]
[Meta<ForSchema>(SCHEMA_KIND_ENUM)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CONSTRAINT}.enum")]
[Meta<Default>(true)]
[Meta<InVisible>(true)] // root only
[Meta<Static>(true)]
public class EnumValue: Property<bool>, IConstraintProperty
{
    public override bool HasValue => true;

    public async Task<bool?> ValidateEnumAsync(SchemaContext context, EnumNode node)
    {
        if (node.IsEmpty) return null;
        var type = (node.Type as Runtime.EnumType)!;
        return type.Type == EnumValueType.Flags
            ? node.TryGetValue(out long flagsValue) && flagsValue >= 0 && flagsValue <= type.MaxFlags
            : await type.GetEnumEntryAccessAsync(context, node.GetValue<string>()) is { Length: > 0};
    }
}