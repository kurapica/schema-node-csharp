using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property.Core;
using SchemaNode.Property.Int;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Array;

/// <summary>
/// The max size constraint property for array data nodes.
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_ARRAY, SCHEMA_KIND_ARRAY_USAGE)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_ARRAY}.{nameof(MaxSize)}")]
[Meta<LowLimitInt>(0L)]
public class MaxSize : Property<long>, IConstraintProperty
{
    /// <summary>
    /// Validate the array data node with sync mode
    /// </summary>
    public virtual bool? ValidateArray(SchemaContext context, ArrayNode node)
    {
        if (!HasValue) return null;
        return node.Count <= Value;
    }

    /// <summary>
    /// Validate the array data node with async mode
    /// </summary>
    public Task<bool?> ValidateArrayAsync(SchemaContext context, ArrayNode node) => Task.FromResult(ValidateArray(context, node));
}