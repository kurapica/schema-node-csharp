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
    public virtual bool? Validate(SchemaContext context, DataNode node)
    {
        return !node.IsEmpty;
    }
}
