using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Function;
using SchemaNode.Node;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Relation;
using SchemaNode.Schema;
using SchemaNode.Struct;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Constraint;

[Meta<ForSchema>(SCHEMA_KIND_STRUCT_FIELD)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CONSTRAINT}.{nameof(BlackList)}")]
[Relation<Visible, Call>(nameof(BlackList), NS_SYSTEM_SCHEMA_REFLECT_IS_SCHEMA_KIND, $"@{nameof(StructFieldSchema.Type)}", true, SCHEMA_KIND_ENUM, SCHEMA_KIND_INT, SCHEMA_KIND_DECIMAL, SCHEMA_KIND_STRING)]
[Relation<OverrideType, Call>(nameof(BlackList), $"{NS_SYSTEM_SCHEMA_REFLECT_ARRAY}.{nameof(SchemaNode.Function.Reflect.Array.getarraytype)}", $"@{nameof(StructFieldSchema.Type)}")]
[Relation<Root, Call>(nameof(BlackList), $"{NS_SYSTEM_INTRINSIC}.assign", "@root")]
[Relation<Cascade, Call>(nameof(BlackList), $"{NS_SYSTEM_INTRINSIC}.assign", "@cascade")]
public class BlackList : Property<object[]>, IConstraintProperty
{
    /// <inheritdoc/>
    public async Task<bool?> ValidateEnumAsync(SchemaContext context, EnumNode node)
    {
        if (Value == null || Value.Length == 0 || node.IsEmpty) return null;
        EntryAccess<string>[] accessList = await (node.Type as Runtime.EnumType)!.GetEnumEntryAccessAsync(context, node.GetValue<string>()!);
        return accessList.All(a => Value.All(v => !v.Equals(a.Entry?.Value)));
    }

    /// <inheritdoc/>
    public bool? ValidateInt(SchemaContext context, IntNode node)
    {
        if (Value == null || Value.Length == 0 || node.IsEmpty) return null;
        return Value.All(v => !v.Equals(node.GetValue<string>()));
    }

    /// <inheritdoc/>
    public bool? ValidateString(SchemaContext context, StringNode node)
    {
        if (Value == null || Value.Length == 0 || node.IsEmpty) return null;
        return Value.All(v => !v.Equals(node.GetValue<string>()));
    }
}