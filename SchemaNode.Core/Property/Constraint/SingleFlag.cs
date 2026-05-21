using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property.Schema;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Constraint;

/// <summary>
/// Don't allow flags enum value combination.
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_STRUCT_FIELD)]
[Meta<ForSchema>(typeof(EnumType))]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY}.{nameof(SingleFlag)}")]
public class SingleFlag : Property<bool>, IConstraintProperty
{
    public bool? ValidateEnum(SchemaContext context, EnumNode node)
    {
        if (!Value || node.IsEmpty) return null;

        // single flag means only one bit should be set
        if (node.TryGetValue<long>(out var val))
            return val != 0 && (val & (val - 1)) == 0;

        return null;
    }
}
