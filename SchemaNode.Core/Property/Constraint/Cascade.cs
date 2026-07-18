using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;
using EnumType = SchemaNode.Runtime.EnumType;

namespace SchemaNode.Property.Constraint;

/// <summary>
/// Limit the enum's cascade level
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_STRUCT_FIELD)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CONSTRAINT}.{nameof(Cascade)}")]
[Relation<Visible, Relation.Call>(NODE_SELF, NS_SYSTEM_SCHEMA_REFLECT_IS_VALUE_KIND, $"@{nameof(StructFieldSchema.Type)}", SCHEMA_KIND_ENUM)]
public class Cascade : Property<long>, IConstraintProperty
{
    public async Task<bool?> ValidateEnumAsync(SchemaContext context, EnumNode node)
    {
        var effectiveValue = Value;
        if (effectiveValue <= 0 || node.IsEmpty) return null;
        EnumType? enumType = node.Type as EnumType;
        if (enumType?.Cascade == null || enumType.Cascade.Length <= effectiveValue) return null;

        EnumValueAccess[] access = await enumType.LoadEnumAccessListAsync(context, node.GetValue<string>()!, noSubList: true, withSubList: false);
        return access.Length <= effectiveValue;
    }
}