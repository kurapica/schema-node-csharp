using SchemaNode.Context;
using SchemaNode.Property;
using SchemaNode.Utility;

namespace SchemaNode.Runtime;

/// <summary>
/// The relation provider
/// </summary>
public interface IRelationProvider
{
    IEnumerable<RelationType> GetRelations();
}

public static class RelationProviderExtension
{
    public static async Task ValidateWithRelationsAsync(this IRelationProvider provider, SchemaContext context, IValueAccess node)
    {
        // Validate by relations
        foreach (RelationType process in provider.GetRelations().Where(r => r.Property?.GetCsharpType()?.IsAssignableTo(typeof(IConstraintProperty)) == true))
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
                string path = spans.Current.ToString();
                List<IValueAccess> nextLevels = [];
                foreach (var currNode in currNodes)
                {
                    if (currNode is IEnumerable<IValueAccess> arr)
                    {
                        foreach (var element in arr)
                        {
                            var next = element.GetAccessValue(path);
                            if (next != null) nextLevels.Add(next);
                        }
                    }
                    else
                    {
                        var next = currNode.GetAccessValue(path);
                        if (next != null) nextLevels.Add(next);
                    }
                }
                currNodes = nextLevels;
            }
        }
    }
}