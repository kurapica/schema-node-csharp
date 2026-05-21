using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property.Schema;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;
using EnumType = SchemaNode.Schema.EnumType;

namespace SchemaNode.Property.Constraint;

/// <summary>
/// Restrict the enum value to be a descendant of the specified root value.
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_STRUCT_FIELD)]
[Meta<ForType>(typeof(EnumType))]
public class Root: Property<string>, IConstraintProperty
{
    public async Task<bool?> ValidateEnumAsync(SchemaContext context, EnumNode node)
    {
        string? nodeValue = node.GetValue<string>();
        if (string.IsNullOrWhiteSpace(Value) || string.IsNullOrWhiteSpace(nodeValue)) return null;
        if (Value.Equals(nodeValue)) return true;

        EnumValueAccess[] access = await (node.Type as Runtime.EnumType)!.LoadEnumAccessListAsync(context, nodeValue, noSubList: true);
        return access.Any(a => a.Value.Equals(Value));
    }
}