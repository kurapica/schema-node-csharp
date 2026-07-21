using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Constraint;

[Meta<ForSchema>(SCHEMA_KIND_ARRAY)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CONSTRAINT}.{nameof(System.Array)}")]
[Meta<Default>(true)]
[Meta<InVisible>(true)] // root only
public class Array: Property<bool>, IConstraintProperty
{
    public override bool HasValue => true;

    public async Task<bool?> ValidateArrayAsync(SchemaContext context, ArrayNode node)
    {
        ArrayType type = (node.Type as ArrayType)!;
        if (type.Element == null) return null;
        
        // Validate by elements
        foreach (IValueAccess element in result)
            await Element.ValidateValueAsync(context, element);

        // Validate by relations
        if (_relations != null)
        {
            foreach (RelationType process in _relations.Where(r => r.Property?.GetCsharpType()?.IsAssignableTo(typeof(IConstraintProperty)) == true))
            {
                // apply constraint on target
                SpanReader spans = process.Target;
                List<DataNode> currNodes = [result];
                while (spans.NextPath())
                {
                    if (spans.IsEnd)
                    {
                        foreach (DataNode currNode in currNodes)
                        {
                            if (await process.ProcessAsync(context, result, currNode) is not IConstraintProperty prop) continue;

                            if (await prop.ValidateAsync(context, currNode) == false)
                            {
                                if (currNode.Violated != null && currNode.Violated.Contains(prop.Name)) continue;
                                currNode.SetViolated(prop);
                            }
                            else if (currNode.Violated != null && currNode.Violated.Contains(prop.Name))
                            {
                                currNode.ClearViolated(prop);
                            }
                        }
                        break;
                    }
                    
                    // Gather effect nodes
                    ReadOnlySpan<char> path = spans.Current;
                    List<DataNode> nextLevels = [];
                    foreach (DataNode currNode in currNodes)
                    {
                        if (currNode is ArrayNode arr)
                        {
                            foreach (DataNode element in arr)
                            {
                                DataNode? next = element.GetAccessValue(path);
                                if (next != null) nextLevels.Add(next);
                            }
                        }
                        else
                        {
                            DataNode? next = currNode.GetAccessValue(path);
                            if (next != null) nextLevels.Add(next);
                        }
                    }
                    currNodes = nextLevels;
                }
            }
        }
    }
}