using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Utility;
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

        // Validate by relations
        foreach (RelationType process in type.GetRelations().Where(r => r.Property?.GetCsharpType()?.IsAssignableTo(typeof(IConstraintProperty)) == true))
        {
            // apply constraint on target
            SpanReader spans = process.Target;
            List<IValueAccess> currNodes = [node];
            while (spans.NextPath())
            {
                if (spans.IsEnd)
                {
                    foreach (var currNode in currNodes)
                    {
                        if (await process.ProcessAsync(context, node, currNode) is not IConstraintProperty prop) continue;
                        bool? result = await prop.ValidateAsync(context, currNode);
                        if (result.HasValue) currNode.RecordConstraint(prop, result.Value);
                    }
                    break;
                }
                
                // Gather effect nodes
                ReadOnlySpan<char> path = spans.Current;
                List<IValueAccess> nextLevels = [];
                foreach (var currNode in currNodes)
                {
                    if (currNode is IEnumerable<IValueAccess> arr)
                    {
                        foreach (var element in arr)
                        {
                            var next = element.GetAccessValue(path.ToString());
                            if (next != null) nextLevels.Add(next);
                        }
                    }
                    else
                    {
                        var next = currNode.GetAccessValue(path.ToString());
                        if (next != null) nextLevels.Add(next);
                    }
                }
                currNodes = nextLevels;
            }
        }
        return null;
    }
}