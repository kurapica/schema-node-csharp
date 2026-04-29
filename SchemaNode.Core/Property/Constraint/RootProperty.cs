using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;

namespace SchemaNode.Property.Constraint;

/// <summary>
/// Restrict the enum value to be a descendant of the specified root value.
/// </summary>
[SchemaProperty([NodeType.StructField], [ValueSchemaType.Enum, ValueSchemaType.Namespace], optionDepends: [nameof(RequireProperty)])]
public class RootProperty : SchemaProperty<Node.DataNode>, IConstraintProperty
{
    public async Task<bool?> ValidateEnumAsync(SchemaContext context, EnumNode node, StructNode? parent = null, Node.DataNode? overrideValue = null)
    {
        var effectiveNode = overrideValue ?? Value;
        if (effectiveNode is not EnumNode enumNode || enumNode.IsEmpty || node.IsEmpty) return null;

        string root = enumNode.ToString();
        string nodeValue = node.ToString();
        if (root.Equals(nodeValue)) return true;

        EnumValueAccess[] access = await (node.NodeType as EnumType)!.LoadEnumAccessListAsync(context, nodeValue, noSubList: true);
        return access.Any(a => a.Value.Equals(root));
    }
}