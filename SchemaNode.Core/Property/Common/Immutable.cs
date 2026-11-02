using SchemaNode.Attribute;
using SchemaNode.Function;
using SchemaNode.Property.Core;
using SchemaNode.Property.Property;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Common;

/// <summary>
/// The data is immutable, means it can't be changed after it has original value
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_NODE_PROPERTY)]
[Meta<OfNodeKind>(NODE_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_COMMON}.{nameof(Immutable)}")]
[Meta<Static>(true)]
public class Immutable : Property<bool>
{
    /// <inheritdoc/>
    public override void Effect(IValueAccess target)
    {
        Clear(target);
        target.RecordSubscription(target.Subscribe(_ =>
        {
            object? original = target.GetOriginal(typeof(object));
            target.SetPropertyValue(typeof(ReadOnly), SystemLogic.isempty(original) ? null : true, target, null);
        }, true), this);
    }

    /// <inheritdoc/>
    public override void Clear(IValueAccess target) => target.ClearSubscription(this);
}
