using SchemaNode.Context;
using SchemaNode.Attribute;
using SchemaNode.Node;
using SchemaNode.Property.Schema;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Constraint;

[Meta<ForSchema>(SCHEMA_KIND_STRUCT_FIELD)]
[Meta<ForType>(typeof(EnumType), typeof(StringType), typeof(IntType))]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY}.{nameof(BlackList)}")]
public class BlackList : Property<object[]>, IConstraintProperty
{
    /// <inheritdoc/>
    public async Task<bool?> ValidateEnumAsync(SchemaContext context, EnumNode node)
    {
        if (Value == null || Value.Length == 0 || node.IsEmpty) return null;
        var accessList = await (node.Type as Runtime.EnumType)!.LoadEnumAccessListAsync(context, node.GetValue<string>()!);
        return accessList.All(a => Value.All(v => !v.Equals(a.Value)));
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