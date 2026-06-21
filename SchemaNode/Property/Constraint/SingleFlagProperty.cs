using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;

namespace SchemaNode.Property.Constraint;

/// <summary>
/// Don't allow flags enum value combination.
/// </summary>
[SchemaProperty([SchemaType.StructField], [ValueSchemaType.FlagsEnum], optionDepends: [nameof(RequireProperty)])]
public class SingleFlagProperty : SchemaProperty<bool>, IConstraintProperty
{
    public bool? ValidateEnum(SchemaContext context, EnumTypeNode node, StructTypeNode? parent = null, AnySchemaNode? overrideValue = null)
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
