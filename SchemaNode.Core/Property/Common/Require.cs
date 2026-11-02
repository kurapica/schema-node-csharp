using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Common;

/// <summary>
/// The node data is required.
/// </summary>
[Meta<OfNodeKind>(NODE_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_COMMON}.{nameof(Require)}")]
public class Require : Property<bool>, IConstraintProperty
{
    /// <inheritdoc/>
    public virtual bool? Validate(SchemaContext context, IValueAccess node)
    {
        if (!HasValue) return null;
        return Value != true
            || !node.IsEmpty
            || node.GetProperty<InVisible>()?.Value == true
            || node.GetProperty<Visible>()?.Value == false;
    }

    /// <inheritdoc/>
    public virtual Task<bool?> ValidateAsync(SchemaContext context, IValueAccess node)
        => Task.FromResult(Validate(context, node));

    /// <inheritdoc/>
    public override void Effect(IValueAccess target)
    {
        Clear(target);
        target.RecordSubscription(target.SubscribeProperty<Visible, bool>(_ => Refresh(target)), this);
        target.RecordSubscription(target.SubscribeProperty<InVisible, bool>(_ => Refresh(target)), this);
    }

    /// <inheritdoc/>
    public override void Clear(IValueAccess target) => target.ClearSubscription(this);

    /// <summary>
    /// Refresh the constraint result
    /// </summary>
    private void Refresh(IValueAccess target)
    {
        if (target is not DataNode node || node.Context is not SchemaContext context) return;
        target.RecordConstraint(this, Validate(context, target));
    }
}
