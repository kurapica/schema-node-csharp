using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;

namespace SchemaNode.Property.Constraint;

/// <summary>
/// The node data is required.
/// </summary>
[SchemaProperty([SchemaType.StructField], [ValueSchemaType.Scalar, ValueSchemaType.Enum, ValueSchemaType.Array, ValueSchemaType.Struct])]
public class RequireProperty : SchemaProperty<bool>, IConstraintProperty
{
    bool? ValidateAny(SchemaContext context, Node.DataNode node, StructNode? parent = null, Node.DataNode? overrideValue = null)
    {
        if ((overrideValue?.ToValue<bool>() ?? Value) != true || parent == null) return null;
        return !node.IsEmpty;
    }

    /// <inheritdoc/>
    public bool? ValidateScalar(SchemaContext context, ScalarNode node, StructNode? parent = null, Node.DataNode? overrideValue = null)
    {
        return ValidateAny(context, node, parent, overrideValue);
    }

    /// <inheritdoc/>
    public bool? ValidateEnum(SchemaContext context, EnumNode node, StructNode? parent = null, Node.DataNode? overrideValue = null)
    {
        return ValidateAny(context, node, parent, overrideValue);
    }

    /// <inheritdoc/>
    public bool? ValidateArray(SchemaContext context, ArrayNode node, StructNode? parent = null, Node.DataNode? overrideValue = null)
    {
        return ValidateAny(context, node, parent, overrideValue);
    }

    /// <inheritdoc/>
    public bool? ValidateStruct(SchemaContext context, StructNode node, StructNode? parent = null, Node.DataNode? overrideValue = null)
    {
        return ValidateAny(context, node, parent, overrideValue);
    }
}
