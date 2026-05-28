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
/// Only allow leaf level enum values to be selected.
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_STRUCT_FIELD)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CONSTRAINT}.{nameof(LeafOnly)}")]
[Relation<Visible>(NS_SYSTEM_SCHEMA_REFLECT_IS_VALUE_KIND, $"${nameof(StructFieldSchema.Type)}", SCHEMA_KIND_ENUM)]
public class LeafOnly : Property<bool>, IConstraintProperty
{
    public async Task<bool?> ValidateEnumAsync(SchemaContext context, EnumNode node)
    {
        if (!Value || node.IsEmpty) return null;
        EnumValueSchema[]? val = (node.Type as EnumType) is { } enumType ? await enumType.LoadEnumSubListAsync(context, node.GetValue<string>()) : null;
        return val == null || val.Length == 0;
    }
}
