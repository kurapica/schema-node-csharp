using SchemaNode.Attribute;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Property;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.String;

/// <summary>
/// The schema kind resolver, used to get the schema kind by KindProvider as the default value of the owner
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_NODE_STRING)]
[Meta<OfNodeKind>(NODE_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_STRING}.{nameof(KindResolver)}")]
[Meta<Static>(true)]
[Meta<ReadOnly>(true)]
[Meta<InVisible>(true)]
public class KindResolver: Property<bool>
{
    /// <inheritdoc/>
    public override void Effect(IValueAccess target)
    {
        if (Value != true) return;

        // defer the effect, so the parent chain is available after the node is attached
        _ = Task.Run(async () =>
        {
            await Task.Yield();
            IValueAccess? provider = target;
            while (provider is not null)
            {
                string? kind = provider.GetProperty<KindProvider>()?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(kind))
                {
                    target.SetPropertyValue<Default, object>(kind, provider, null);
                    break;
                }
                provider = provider.Parent;
            }
        });
    }
}
