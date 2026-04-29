using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;

namespace SchemaNode.Property.Constraint;

/// <summary>
/// Only allow leaf level enum values to be selected.
/// </summary>
[SchemaProperty([NodeType.StructField], [ValueSchemaType.Enum], includeArray: true, optionDepends: [nameof(RequireProperty)])]
public class LeafOnlyProperty : SchemaProperty<bool>, IConstraintProperty
{
    public async Task<bool?> ValidateEnumAsync(SchemaContext context, EnumNode node, StructNode? parent = null, Node.DataNode? overrideValue = null)
    {
        if ((overrideValue?.ToValue<bool>() ?? Value) != true || node.IsEmpty) return null;
        EnumValueSchema? val = (node.NodeType as EnumType) is { } enumType ? await enumType.LoadEnumValueInfo(context, node.Value?.ToString() ?? "") : null;
        return val != null && val.HasSubList != true;
    }
}
