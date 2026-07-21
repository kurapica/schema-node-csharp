using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Constraint;

[Meta<ForSchema>(SCHEMA_KIND_STRUCT)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CONSTRAINT}.{nameof(Struct)}")]
[Meta<Default>(true)]
[Meta<InVisible>(true)] // root only
public class Struct: Property<bool>, IConstraintProperty
{
    public override bool HasValue => true;

    public async Task<bool?> ValidateStructAsync(SchemaContext context, StructNode node)
    {
        StructType type = (node.Type as StructType)!;
        
        // Validate by fields
        foreach (var field in type.GetFields().Where(f => f.Type != null && f.DisplayOnly != true))
        {
            if (node.GetAccessValue(field.Name) is not {} dataNode) continue;
            
            // validate the fields
            foreach (IConstraintProperty constraint in field.Constraints)
            {
                bool? result = await constraint.ValidateAsync(context, dataNode);
                if (result.HasValue) dataNode.RecordConstraint(constraint, result.Value);
            }
        }

        // validate by relations
        await type.ValidateWithRelationsAsync(context, node);
        return null;
    }
}