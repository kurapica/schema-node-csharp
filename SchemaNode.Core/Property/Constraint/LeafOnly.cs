using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Schema;
using SchemaNode.Struct;
using static SchemaNode.Utility.Constant;
using EnumType = SchemaNode.Runtime.EnumType;

namespace SchemaNode.Property.Constraint;

/// <summary>
/// Only allow leaf level enum values to be selected.
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_STRUCT_FIELD)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CONSTRAINT}.{nameof(LeafOnly)}")]
[Relation<Visible, Relation.Call>(NODE_SELF, NS_SYSTEM_SCHEMA_REFLECT_IS_VALUE_KIND, $"@{nameof(StructFieldSchema.Type)}", SCHEMA_KIND_ENUM)]
public class LeafOnly : Property<bool>, IConstraintProperty
{
    public async Task<bool?> ValidateEnumAsync(SchemaContext context, EnumNode node)
    {
        if (!Value || node.IsEmpty) return null;
        EntryAccess<string>[]? val = (node.Type as EnumType) is { } enumType ? await enumType.GetEnumEntryAccess(context, null, node.GetValue<string>()) : null;
        if (val is null || val.Length == 0) return null;
        return val[0].Entry != null && val[0].Entry!.HasChildren != true;
    }
}
