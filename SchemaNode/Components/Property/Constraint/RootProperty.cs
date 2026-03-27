using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;

namespace SchemaNode.Components.Property.Constraint;

/// <summary>
/// Restrict the enum value to be a descendant of the specified root value.
/// </summary>
[SchemaProperty([SchemaType.StructField], [ValueSchemaType.Enum, ValueSchemaType.Namespace], optionDepends: [nameof(RequireProperty)])]
public class RootProperty : SchemaProperty<AnySchemaNode>, IConstraintProperty
{
    public async Task<bool?> ValidateEnumAsync(SchemaContext context, EnumTypeNode node, StructTypeNode? parent = null)
    {
        if (Value is not EnumTypeNode enumNode || enumNode.IsEmpty || node.IsEmpty) return null;

        string root = enumNode.ToString();
        string nodeValue = node.ToString();
        if (root.Equals(nodeValue)) return true;

        EnumValueAccess[] access = await (node.SchemaType as EnumType)!.LoadEnumAccessListAsync(context, nodeValue, noSubList: true);
        return access.Any(a => a.Value.Equals(root));
    }
}