using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Constraint;

/// <summary>
/// Don't allow flags enum value combination.
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_STRUCT_FIELD)]
public class SingleFlag : Property<bool>, IConstraintProperty
{
    public bool? ValidateEnum(SchemaContext context, EnumNode node, StructNode? parent = null, Node.DataNode? overrideValue = null)
    {
        if ((overrideValue?.ToValue<bool>() ?? Value) != true || node.IsEmpty) return null;

        // single flag means only one bit should be set
        if (node.Value is int intVal)
            return intVal != 0 && (intVal & (intVal - 1)) == 0;
        if (node.Value is long longVal)
            return longVal != 0 && (longVal & (longVal - 1)) == 0;

        return null;
    }
}
