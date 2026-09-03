using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property.Common;
using SchemaNode.Property.Property;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Array;

[Meta<Alias>("array")]
[Meta<ForSchema>(SCHEMA_KIND_ARRAY)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_ARRAY}.valid")]
[Meta<Default>(true)]
[Meta<InVisible>(true)] // root only
public class ArrayValue: Property<bool>, IConstraintProperty
{
    public override bool HasValue => true;

    public async Task<bool?> ValidateArrayAsync(SchemaContext context, ArrayNode node)
    {
        ArrayType type = (node.Type as ArrayType)!;
        if (type.Element == null) return null;

        // Validate by elements
        foreach (IConstraintProperty prop in type.Constraints)
        {
            foreach (IValueAccess element in node)
            {
                bool? result = await prop.ValidateAsync(context, element);
                if (result.HasValue) element.RecordConstraint(prop, result.Value);
            }
        }

        // Validate by relations
        await type.ValidateWithRelationsAsync(context, node);
        return null;
    }
}