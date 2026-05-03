using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property.Schema;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Constraint;

/// <summary>
/// Only allow leaf level enum values to be selected.
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_STRUCT_FIELD)]
[Meta<OptionDepends>(typeof(Require))]
public class LeafOnly : Property<bool>, IConstraintProperty
{
    public async Task<bool?> ValidateEnumAsync(SchemaContext context, EnumNode node, StructNode? parent = null, Node.DataNode? overrideValue = null)
    {
        if ((overrideValue?.ToValue<bool>() ?? Value) != true || node.IsEmpty) return null;
        EnumValueSchema? val = (node.NodeType as EnumType) is { } enumType ? await enumType.LoadEnumValueInfo(context, node.Value?.ToString() ?? "") : null;
        return val != null && val.HasSubList != true;
    }
}
