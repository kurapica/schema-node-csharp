using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property.Schema;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Constraint;

/// <summary>
/// Restrict the enum value to be a descendant of the specified root value.
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_STRUCT_FIELD)]
public class Root: Property<Node.DataNode>, IConstraintProperty
{
    public async Task<bool?> ValidateEnumAsync(SchemaContext context, EnumNode node, StructNode? parent = null, Node.DataNode? overrideValue = null)
    {
        var effectiveNode = overrideValue ?? Value;
        if (effectiveNode is not EnumNode enumNode || enumNode.IsEmpty || node.IsEmpty) return null;

        string root = enumNode.ToString();
        string nodeValue = node.ToString();
        if (root.Equals(nodeValue)) return true;

        EnumValueAccess[] access = await (node.Type as EnumType)!.LoadEnumAccessListAsync(context, nodeValue, noSubList: true);
        return access.Any(a => a.Value.Equals(root));
    }
}