using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Property;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.String;

/// <summary>
/// The access value type resolver
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_NODE_STRING)]
[Meta<OfNodeKind>(NODE_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_STRING}.{nameof(AccessValueTypeResolver)}")]
[Meta<Static>(true)]
[Meta<ReadOnly>(true)]
[Meta<InVisible>(true)]
public class AccessValueTypeResolver: Property<string>
{
    /// <inheritdoc/>
    public override void Effect(IValueAccess target)
    {
        Clear(target);

        if (string.IsNullOrWhiteSpace(Value)) return;

        // defer the effect, so the parent chain is available after the node is attached
        _ = Task.Run(async () =>
        {
            await Task.Yield();
            if (target is not DataNode node || node.Context is not SchemaContext context) return;

            IValueAccess? provider = target;
            IValueAccess? source = Source ?? target.Parent;

            // find the access value provider
            while (provider is not null)
            {
                AccessValueTypeProvider? accessProvider = provider.GetProperty<AccessValueTypeProvider>();
                FuncCall? accessCall = accessProvider?.Value;
                if (!string.IsNullOrWhiteSpace(accessCall?.Func))
                {
                    async Task ResolveAsync()
                    {
                        FunctionType? func = await context.GetNodeTypeAsync<FunctionType>(accessCall!.Func);
                        if (func is null) return;

                        object?[] args = accessCall.Args.Select(a =>
                        {
                            if (string.IsNullOrWhiteSpace(a.Source)) return a.Value;
                            IValueAccess? accessed = a.Source == NODE_SELF
                                ? source?.GetAccessValue(Value!, target)
                                : provider.GetAccessValue(a.Source, target);
                            return accessed?.GetValue<object>();
                        }).ToArray();

                        string? value = await func.CallAsync<string>(context, args);
                        target.SetPropertyValue<Default, object>(value, provider, null);
                    }

                    // subscribe the access value provider
                    foreach (CallArg arg in accessCall.Args)
                    {
                        if (string.IsNullOrWhiteSpace(arg.Source)) continue;
                        IValueAccess? accessed = arg.Source == NODE_SELF
                            ? source?.GetAccessValue(Value!, target)
                            : provider.GetAccessValue(arg.Source, target);
                        if (accessed is not null)
                            target.RecordSubscription(accessed.Subscribe(_args => { _ = ResolveAsync(); }), this);
                    }

                    // resolve the access value type
                    await ResolveAsync();
                    break;
                }
                provider = provider.Parent;
            }
        });
    }

    /// <inheritdoc/>
    public override void Clear(IValueAccess target) => target.ClearSubscription(this);
}
