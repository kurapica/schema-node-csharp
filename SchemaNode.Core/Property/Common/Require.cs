using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Common;

/// <summary>
/// The node data is required.
/// </summary>
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_COMMON}.{nameof(Require)}")]
public class Require : Property<bool>, IConstraintProperty
{
    public virtual bool? Validate(SchemaContext context, IValueAccess node)
    {
        return !node.IsEmpty;
    }
}
