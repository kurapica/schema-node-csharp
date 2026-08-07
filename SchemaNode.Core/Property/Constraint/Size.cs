using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Function;
using SchemaNode.Node;
using SchemaNode.Property.Core;
using SchemaNode.Relation;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Constraint;

/// <summary>
/// The min size constraint property for array data nodes.
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_ARRAY)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CONSTRAINT}.{nameof(MinSize)}")]
[Meta<LowLimitInt>(0L)]
[Relation<UpLimitInt, Call>($"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(MaxSize)}")]
public class MinSize : Property<long>, IConstraintProperty
{
    /// <summary>
    /// Validate the array data node with sync mode
    /// </summary>
    public virtual bool? ValidateArray(SchemaContext context, ArrayNode node)
    {
        if (!HasValue) return null;
        return node.Count >= Value ? true : false;
    }

    /// <summary>
    /// Validate the array data node with async mode
    /// </summary>
    public Task<bool?> ValidateArrayAsync(SchemaContext context, ArrayNode node) => Task.FromResult(ValidateArray(context, node));
}

/// <summary>
/// The max size constraint property for array data nodes.
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_ARRAY)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CONSTRAINT}.{nameof(MaxSize)}")]
[Meta<LowLimitInt>(0L)]
public class MaxSize : Property<long>, IConstraintProperty
{
    /// <summary>
    /// Validate the array data node with sync mode
    /// </summary>
    public virtual bool? ValidateArray(SchemaContext context, ArrayNode node)
    {
        if (!HasValue) return null;
        return node.Count <= Value ? true : false;
    }

    /// <summary>
    /// Validate the array data node with async mode
    /// </summary>
    public Task<bool?> ValidateArrayAsync(SchemaContext context, ArrayNode node) => Task.FromResult(ValidateArray(context, node));
}
