using SchemaNode.Context;
using SchemaNode.Node;

namespace SchemaNode.Property;

/// <summary>
/// The interface for constraint property components that can be attached to schemas. 
/// It defines the validation logic for the constraint rule. 
/// Each constraint property component should implement the Validate method for the applicable schema types, and return true if valid, false if invalid, null if not applicable.
/// </summary>
public interface IConstraintProperty: IProperty
{
    /// <summary>
    /// Validate the data node with the constraint rule. Return true if valid, false if invalid, null if not applicable.
    /// </summary>
    public virtual bool? Validate(SchemaContext context, IDataNode node) => null;

    /// <summary>
    /// Async version of <see cref="Validate"/>. Override this for async constraint validation.
    /// </summary>
    public virtual Task<bool?> ValidateAsync(SchemaContext context, IDataNode node) => Task.FromResult(Validate(context, node));

    /// <summary>
    /// Validate the array data node
    /// </summary>
    public virtual async Task<bool?> ValidateArrayAsync(SchemaContext context, ArrayNode node)
    {
        if (!HasValue || node.IsEmpty) return null;
        foreach (var item in node)
        {
            if ((await ValidateAsync(context, item)) == false)
                return false;
        }
        return null;
    }
}