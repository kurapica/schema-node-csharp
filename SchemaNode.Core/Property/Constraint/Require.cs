using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Constraint;

/// <summary>
/// The node data is required.
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_STRUCT_FIELD)]
public class Require : Property<bool>, IConstraintProperty
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
