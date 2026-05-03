using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property.Schema;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Constraint;

/// <summary>
/// Limit the enum's cascade level
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_STRUCT_FIELD)]
[Meta<OptionDepends>(typeof(Require))]
public class Cascade : Property<long>, IConstraintProperty
{
    public async Task<bool?> ValidateEnumAsync(SchemaContext context, EnumNode node, StructNode? parent = null, Node.DataNode? overrideValue = null)
    {
        var effectiveValue = overrideValue?.ToValue<long>() ?? Value;
        if (effectiveValue <= 0 || node.IsEmpty) return null;
        EnumType? enumType = node.NodeType as EnumType;
        if (enumType?.Cascade == null || enumType.Cascade.Length <= effectiveValue) return null;

        var access = await enumType.LoadEnumAccessListAsync(context, node.Value!.ToString()!, noSubList: true, withSubList: false);
        return access.Length <= effectiveValue;
    }
}